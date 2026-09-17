using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf;
using ArchiveB1;
using b1;

namespace BmwAchievementViewer
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            string htmlOutput = "report/result.html";
            List<string> files = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--html" || args[i] == "-h")
                {
                    if (i + 1 >= args.Length)
                    {
                        Console.Error.WriteLine("--html 需要指定输出文件或目录。用法: BmwAchievementViewer <save.sav> --html <report.html|report_dir>");
                        return 2;
                    }
                    htmlOutput = args[++i];
                }
                else if (args[i] == "--help")
                {
                    PrintHelp();
                    return 0;
                }
                else
                {
                    files.Add(args[i]);
                }
            }

            if (files.Count == 0)
            {
                files = FindDefaultSaves().ToList();
            }
            if (files.Count == 0)
            {
                Console.Error.WriteLine("未找到存档文件。用法: BmwAchievementViewer <save1.sav> [save2.sav ...] [--html <report.html|report_dir>]");
                return 2;
            }

            var reports = new List<(string SavePath, FUStBEDArchivesData Data, string OuterMeta)>();
            foreach (string savePath in files)
            {
                try
                {
                    var info = LoadSave(savePath);
                    reports.Add((savePath, info.Data, info.OuterMeta));
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"处理 {savePath} 失败: {ex.Message}");
                    return 1;
                }
            }

            if (htmlOutput != null)
            {
                WriteHtmlReports(reports, htmlOutput);
            }

            int code = 0;
            foreach (var r in reports)
            {
                try
                {
                    PrintSave(r.SavePath, r.Data, r.OuterMeta);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"输出 {r.SavePath} 失败: {ex.Message}");
                    code = 1;
                }
                Console.WriteLine();
            }
            return code;
        }

        private static void PrintHelp()
        {
            Console.WriteLine("黑神话：悟空 存档成就查看器");
            Console.WriteLine();
            Console.WriteLine("用法:");
            Console.WriteLine("  BmwAchievementViewer [<save1.sav> ...] [选项]");
            Console.WriteLine();
            Console.WriteLine("参数:");
            Console.WriteLine("  <存档路径>         一个或多个 .sav 存档文件（缺省时扫描工作目录及子目录）");
            Console.WriteLine("  --html <path>      生成 HTML 报表。path 以 .html 结尾时输出单文件；");
            Console.WriteLine("                     否则视为目录，为每个存档生成报表并附带 index.html 汇总页");
            Console.WriteLine("  --help             显示本帮助");
        }

        private static (FUStBEDArchivesData Data, string OuterMeta) LoadSave(string savePath)
        {
            byte[] bytes = File.ReadAllBytes(savePath);
            ArchiveFile info = new ArchiveFile();
            info.MergeFrom(bytes);
            string outerMeta = $"GameArchivesDataBytes = {info.GameArchivesDataBytes.Length} 字节";
            byte[] contentBytes = info.GameArchivesDataBytes.ToByteArray();
            FUStBEDArchivesData data =
                BGW_GameArchiveMgr.DeserializeArchiveDataFromBytes<FUStBEDArchivesData>(true, contentBytes);
            return (data, outerMeta);
        }

        private static string[] FindDefaultSaves()
        {
            string cwd = Directory.GetCurrentDirectory();
            List<string> saves = new List<string>();
            saves.AddRange(Directory.GetFiles(cwd, "*.sav"));
            foreach (string dir in Directory.GetDirectories(cwd))
            {
                saves.AddRange(Directory.GetFiles(dir, "*.sav"));
            }
            return saves.Distinct().ToArray();
        }

        private static void PrintSave(string savePath, FUStBEDArchivesData data, string outerMeta)
        {
            Console.WriteLine("==============================================");
            Console.WriteLine($"存档: {Path.GetFullPath(savePath)}");
            Console.WriteLine($"外层元数据: {outerMeta}");

            PrintRoleSummary(data);
            PrintAchievements(data);
            PrintMuseum(data);
        }

        private static void PrintRoleSummary(FUStBEDArchivesData data)
        {
            RoleBase b = data.RoleData.RoleCs.Base;
            RoleActor actor = data.RoleData.RoleCs.Actor;
            Console.WriteLine();
            Console.WriteLine("玩家信息:");
            Console.WriteLine($"  名称           : {b.Name}");
            Console.WriteLine($"  等级           : {b.Level}");
            Console.WriteLine($"  RoleId         : {b.Roleid}");
            Console.WriteLine($"  周目(NewGame+) : {actor.NewGamePlusCount}");
        }

        private static void PrintAchievements(FUStBEDArchivesData data)
        {
            RoleAchievement ach = data.RoleData.RoleCs.Achievement;
            Console.WriteLine();
            Console.WriteLine("成就信息:");
            if (ach == null)
            {
                Console.WriteLine("  (无成就数据)");
                return;
            }
            Console.WriteLine($"  当前成就版本: {ach.AchievementVersion}");

            var all = ach.Achievements.OrderBy(x => x.Config.AchievementId).ToList();
            int done = all.Count(x => x.IsComplete);
            Console.WriteLine($"  总数 {all.Count} / 已达成 {done} / 未达成 {all.Count - done}");

            var steamAch = all.Where(x => x.Config.AchievementId >= 81001 && x.Config.AchievementId <= 81081).ToList();
            var internalAch = all.Where(x => x.Config.AchievementId < 81001 || x.Config.AchievementId > 81081).ToList();
            int steamDone = steamAch.Count(x => x.IsComplete);
            Console.WriteLine($"  Steam成就(八十一难): {steamDone}/{steamAch.Count}");

            Console.WriteLine();
            Console.WriteLine("  八十一难成就:");
            if (steamAch.Count == 0)
            {
                Console.WriteLine("    (空)");
            }
            foreach (var a in steamAch)
            {
                int id = a.Config.AchievementId;
                int seq = id - 81000;
                string mark = a.IsComplete ? "[已达成]" : "[未达成]";
                string name = AchievementNames.TryGetValue(id, out string n) ? n : $"未知({id})";
                string reqType = Describe(a.Config.RequirementType.ToString());
                Console.WriteLine($"    {mark} 第{seq}难 {name}  ({reqType}  需求数:{a.Config.RequirementCount})");
                if (a.CompleteRequirementList.Count > 0)
                {
                    var named = a.CompleteRequirementList.Select(x => ResolveRequirement(a.Config.RequirementType.ToString(), x));
                    Console.WriteLine($"       已计数需求: {string.Join(", ", named)}");
                }
            }

            if (internalAch.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"  内部追踪成就: {internalAch.Count} 个");
                foreach (var a in internalAch)
                {
                    int id = a.Config.AchievementId;
                    string mark = a.IsComplete ? "[已达成]" : "[未达成]";
                    string reqType = Describe(a.Config.RequirementType.ToString());
                    Console.WriteLine($"    {mark} ID={id}  ({reqType}  需求数:{a.Config.RequirementCount})");
                    if (a.CompleteRequirementList.Count > 0)
                    {
                        var named = a.CompleteRequirementList.Select(x => ResolveRequirement(a.Config.RequirementType.ToString(), x));
                        Console.WriteLine($"       已计数需求: {string.Join(", ", named)}");
                    }
                }
            }
        }

        private static void PrintMuseum(FUStBEDArchivesData data)
        {
            RoleMuseum museum = data.RoleData.RoleCs.Museum;
            Console.WriteLine();
            Console.WriteLine("影神图/博物馆:");
            if (museum == null)
            {
                Console.WriteLine("  (无数据)");
                return;
            }
            Console.WriteLine($"  动画(Mv): {museum.MvIdList.Count} 个");
            Console.WriteLine($"  配乐: {museum.SoundtrackIdList.Count} 个");

            RoleCollection collection = data.RoleData.RoleCs.Collection;
            if (collection != null)
            {
                Console.WriteLine($"  图鉴妖兽条目: {collection.MonsterCollectionList.Count} 个");
            }
        }

        private static void WriteHtmlReports(List<(string SavePath, FUStBEDArchivesData Data, string OuterMeta)> reports, string htmlOutput)
        {
            bool singleFile = reports.Count == 1 && htmlOutput.EndsWith(".html", StringComparison.OrdinalIgnoreCase);
            string dir;
            if (singleFile)
            {
                dir = Path.GetDirectoryName(Path.GetFullPath(htmlOutput));
                Directory.CreateDirectory(dir);
                WriteHtmlReport(reports[0], Path.GetFullPath(htmlOutput));
            }
            else
            {
                dir = htmlOutput;
                Directory.CreateDirectory(dir);
                var pages = new List<(string SavePath, string FileName, string SteamCount)>();
                foreach (var r in reports)
                {
                    string baseName = Path.GetFileNameWithoutExtension(r.SavePath);
                    string fileName = baseName + ".html";
                    WriteHtmlReport(r, Path.Combine(dir, fileName));
                    pages.Add((r.SavePath, fileName, GetSteamSummary(r.Data)));
                }
                WriteHtmlIndex(dir, pages);
            }
            Console.WriteLine($"已生成 HTML 报表: {Path.GetFullPath(htmlOutput)}");
            string indexPath = singleFile ? Path.GetFullPath(htmlOutput) : Path.Combine(dir, "index.html");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(indexPath) { UseShellExecute = true });
        }

        private static string GetSteamSummary(FUStBEDArchivesData data)
        {
            var ach = data.RoleData.RoleCs.Achievement;
            if (ach == null) return "-";
            var steam = ach.Achievements.Where(x => x.Config.AchievementId >= 81001 && x.Config.AchievementId <= 81081).ToList();
            int done = steam.Count(x => x.IsComplete);
            return $"{done}/{steam.Count}";
        }

        private static void WriteHtmlIndex(string dir, List<(string SavePath, string FileName, string SteamCount)> pages)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"zh-CN\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            sb.AppendLine("<title>黑神话：悟空 存档报表索引</title>");
            sb.AppendLine("<style>");
            sb.AppendLine("body{background:#0d0a07;color:#e8dcc0;font-family:'Songti SC','Noto Serif SC',serif;margin:0;padding:40px 5vw;}");
            sb.AppendLine("h1{color:#f0c869;letter-spacing:4px;}");
            sb.AppendLine("table{border-collapse:collapse;width:100%;margin-top:20px;}");
            sb.AppendLine("th,td{padding:14px 16px;border-bottom:1px solid #3a2f1d;text-align:left;font-size:15px;}");
            sb.AppendLine("th{color:#b09a6a;font-weight:normal;letter-spacing:2px;}");
            sb.AppendLine("a{color:#f0c869;text-decoration:none;}a:hover{color:#ffdf8a;}");
            sb.AppendLine(".pill{display:inline-block;background:#1c160c;border:1px solid #6b5628;border-radius:20px;padding:2px 14px;font-size:14px;color:#f0c869;}");
            sb.AppendLine("</style></head><body>");
            sb.AppendLine("<h1>黑神话：悟空 · 存档报表</h1>");
            sb.AppendLine("<table><tr><th>存档</th><th>Steam成就</th></tr>");
            foreach (var p in pages)
            {
                sb.AppendLine($"<tr><td><a href=\"{HtmlEncode(p.FileName)}\">{HtmlEncode(Path.GetFileName(p.SavePath))}</a></td><td><span class=\"pill\">{HtmlEncode(p.SteamCount)}</span></td></tr>");
            }
            sb.AppendLine("</table></body></html>");
            File.WriteAllText(Path.Combine(dir, "index.html"), sb.ToString(), System.Text.Encoding.UTF8);
        }

        private static void WriteHtmlReport((string SavePath, FUStBEDArchivesData Data, string OuterMeta) r, string outPath)
        {
            var d = r.Data;
            RoleBase b = d.RoleData.RoleCs.Base;
            RoleActor actor = d.RoleData.RoleCs.Actor;
            var ach = d.RoleData.RoleCs.Achievement;
            var museum = d.RoleData.RoleCs.Museum;
            var collection = d.RoleData.RoleCs.Collection;

            var all = ach == null ? new List<AchievementOne>() : ach.Achievements.OrderBy(x => x.Config.AchievementId).ToList();
            var steamAch = all.Where(x => x.Config.AchievementId >= 81001 && x.Config.AchievementId <= 81081).OrderBy(x => x.Config.AchievementId).ToList();
            var internalAch = all.Where(x => x.Config.AchievementId < 81001 || x.Config.AchievementId > 81081).OrderBy(x => x.Config.AchievementId).ToList();
            int allDone = all.Count(x => x.IsComplete);
            int steamDone = steamAch.Count(x => x.IsComplete);
            int steamPct = steamAch.Count == 0 ? 0 : (int)Math.Round(steamDone * 100.0 / steamAch.Count);

            var sb = new System.Text.StringBuilder(64 * 1024);
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"zh-CN\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            sb.AppendFormat("<title>黑神话：悟空 存档 · {0}</title>\n", HtmlEncode(b.Name));
            sb.AppendLine("<style>");
            sb.AppendLine(":root{--gold:#f0c869;--gold-dim:#c9a858;--bg:#0d0a07;--panel:#161009;--line:#3a2f1d;--text:#e8dcc0;--muted:#9a8b6d;--green:#8fbf6f;--green-dim:#5f7f48;--red:#c96a5a;}");
            sb.AppendLine("*{box-sizing:border-box;}");
            sb.AppendLine("body{background:var(--bg);color:var(--text);font-family:'Songti SC','Noto Serif SC','Microsoft YaHei',serif;margin:0;padding:0 0 60px;}");
            sb.AppendLine(".wrap{max-width:1080px;margin:0 auto;padding:0 24px;}");
            sb.AppendLine("header{background:linear-gradient(180deg,#1a1207,#0d0a07);border-bottom:1px solid var(--line);padding:36px 0 24px;margin-bottom:28px;}");
            sb.AppendLine("h1{margin:0;color:var(--gold);letter-spacing:6px;font-size:26px;}");
            sb.AppendLine(".sub{margin-top:6px;color:var(--muted);font-size:13px;letter-spacing:1px;}");
            sb.AppendLine(".card{background:var(--panel);border:1px solid var(--line);border-radius:10px;padding:22px 26px;margin-bottom:26px;}");
            sb.AppendLine(".card h2{margin:0 0 16px;color:var(--gold);font-size:18px;letter-spacing:3px;font-weight:normal;border-left:4px solid var(--gold);padding-left:12px;}");
            sb.AppendLine(".kv{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:18px;}");
            sb.AppendLine(".kv .k{color:var(--muted);font-size:13px;letter-spacing:2px;margin-bottom:4px;}");
            sb.AppendLine(".kv .v{font-size:20px;color:var(--text);}");
            sb.AppendLine(".bar{height:14px;background:#241b0e;border-radius:8px;overflow:hidden;position:relative;}");
            sb.AppendLine(".bar>span{display:block;height:100%;background:linear-gradient(90deg,var(--gold-dim),var(--gold));transition:width .6s;}");
            sb.AppendLine(".stats{display:flex;gap:34px;flex-wrap:wrap;margin-top:18px;}");
            sb.AppendLine(".stats .st{font-size:15px;}.stats .st b{color:var(--gold);font-size:26px;margin-right:8px;}");
            sb.AppendLine(".toolbar{display:flex;gap:10px;align-items:center;margin-bottom:16px;flex-wrap:wrap;}");
            sb.AppendLine(".chip{cursor:pointer;border:1px solid var(--line);background:#1c140a;color:var(--muted);border-radius:20px;padding:5px 16px;font-size:14px;letter-spacing:2px;}");
            sb.AppendLine(".chip.active{color:#1a1207;background:var(--gold);border-color:var(--gold);font-weight:bold;}");
            sb.AppendLine("input[type=search]{background:#1c140a;border:1px solid var(--line);color:var(--text);border-radius:8px;padding:6px 12px;font-size:14px;width:220px;outline:none;}");
            sb.AppendLine("input[type=search]:focus{border-color:var(--gold-dim);}");
            sb.AppendLine(".grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(150px,1fr));gap:12px;}");
            sb.AppendLine(".ach{background:#1c140a;border:1px solid var(--line);border-radius:10px;padding:12px 10px;text-align:center;position:relative;transition:border-color .15s,transform .15s;}");
            sb.AppendLine(".ach:hover{border-color:var(--gold-dim);transform:translateY(-2px);}");
            sb.AppendLine(".ach.done{border-color:#4a6b34;}");
            sb.AppendLine(".ach.done::after{content:'✓';position:absolute;top:-8px;right:-8px;width:22px;height:22px;background:var(--green);color:#0d0a07;border-radius:50%;font-size:13px;line-height:22px;font-weight:bold;box-shadow:0 0 6px rgba(143,191,111,.5);}");
            sb.AppendLine(".ach .no{font-size:11px;color:var(--muted);letter-spacing:1px;margin-bottom:6px;display:block;opacity:.7;}");
            sb.AppendLine(".ach .ord{position:absolute;top:6px;left:8px;font-size:10px;color:#1a1207;background:var(--gold);border-radius:6px;padding:1px 7px;letter-spacing:1px;font-weight:bold;}");
            sb.AppendLine(".ach.done .ord{background:var(--green);}");
            sb.AppendLine(".ach .nm{font-size:15px;letter-spacing:1px;line-height:1.5;min-height:24px;}");
            sb.AppendLine(".ach.done .nm{color:var(--green);}");
            sb.AppendLine(".ach .tp{display:block;margin-top:8px;font-size:11.5px;color:var(--muted);letter-spacing:1px;min-height:16px;}");
            sb.AppendLine(".ach .rq{display:block;margin-top:6px;font-size:10.5px;color:var(--gold-dim);letter-spacing:.3px;line-height:1.5;word-break:break-all;text-align:left;}");
            sb.AppendLine(".ach.hide{display:none;}");
            sb.AppendLine("table.tbl{width:100%;border-collapse:collapse;font-size:13.5px;}");
            sb.AppendLine(".tbl th,.tbl td{padding:9px 12px;border-bottom:1px solid var(--line);text-align:left;color:var(--text);}");
            sb.AppendLine(".tbl th{color:var(--muted);font-weight:normal;letter-spacing:2px;font-size:12.5px;}");
            sb.AppendLine(".tbl td.ids{color:var(--muted);font-size:12px;word-break:break-all;}");
            sb.AppendLine("details summary{cursor:pointer;color:var(--gold-dim);letter-spacing:1px;font-size:14px;background:#1a1207;border:1px solid var(--line);padding:12px 20px;border-radius:10px;}");
            sb.AppendLine("details[open] summary{border-bottom:none;border-radius:10px 10px 0 0;}");
            sb.AppendLine("details .inner{border:1px solid var(--line);border-top:none;border-radius:0 0 10px 10px;padding:14px 20px;}");
            sb.AppendLine("footer{margin-top:40px;color:var(--muted);font-size:12px;text-align:center;letter-spacing:1px;}");
            sb.AppendLine("</style></head><body>");

            sb.AppendLine("<header><div class=\"wrap\">");
            sb.AppendFormat("<h1>黑神话：悟空 · {0}</h1>\n", HtmlEncode(b.Name));
            sb.AppendFormat("<div class=\"sub\">{0} · 等级 {1} · 周目 {2}</div>\n", HtmlEncode(Path.GetFileName(r.SavePath)), b.Level, actor.NewGamePlusCount);
            sb.AppendLine("</div></header>");

            sb.AppendLine("<main class=\"wrap\">");
            sb.AppendLine("<section class=\"card\"><h2>玩家信息</h2><div class=\"kv\">");
            sb.AppendFormat("<div><div class=\"k\">名称</div><div class=\"v\">{0}</div></div>\n", HtmlEncode(b.Name));
            sb.AppendFormat("<div><div class=\"k\">等级</div><div class=\"v\">{0}</div></div>\n", b.Level);
            sb.AppendFormat("<div><div class=\"k\">RoleId</div><div class=\"v\">{0}</div></div>\n", b.Roleid);
            sb.AppendFormat("<div><div class=\"k\">周目</div><div class=\"v\">{0}</div></div>\n", actor.NewGamePlusCount);
            sb.AppendLine("</div>");
            sb.AppendLine("<div class=\"bar\"><span style=\"width:100%\"></span></div>");

            if (ach != null)
            {
                sb.AppendFormat("<div class=\"stats\"><div class=\"st\">Steam成就<div><b>{0}</b><span style=\"color:var(--muted)\">/ {1}</span></div></div>", steamDone, steamAch.Count);
                sb.AppendFormat("<div class=\"st\">成就总数<div><b>{0}</b><span style=\"color:var(--muted)\">/ {1}</span></div></div>", allDone, all.Count);
                sb.AppendFormat("<div class=\"st\">达成率<div><b>{0}%</b></div></div></div>\n", steamPct);
                sb.AppendLine("<div style=\"margin-top:14px\" class=\"bar\"><span style=\"width:" + steamPct + "%\"></span></div>");
            }
            sb.AppendLine("</section>");

            if (ach != null)
            {
                sb.AppendLine("<section class=\"card\"><h2>八十一难</h2>");
                sb.AppendLine("<div class=\"toolbar\">");
                sb.AppendLine("<span class=\"chip active\" data-f=\"all\">全部</span><span class=\"chip\" data-f=\"done\">已达成</span><span class=\"chip\" data-f=\"miss\">未达成</span><input type=\"search\" id=\"q\" placeholder=\"搜索成就名…\">");
                sb.AppendLine("</div>");
                sb.AppendLine("<div class=\"grid\" id=\"grid\">");
                foreach (var a in steamAch)
                {
                    int id = a.Config.AchievementId;
                    int seq = id - 81000;
                    string cls = a.IsComplete ? "ach done" : "ach";
                    string nm = AchievementNames.TryGetValue(id, out string n) ? n : $"未知({id})";
                    string tp = Describe(a.Config.RequirementType.ToString());
                    string reqIds = a.CompleteRequirementList.Count > 0
                        ? string.Join(", ", a.CompleteRequirementList.Select(x => ResolveRequirement(a.Config.RequirementType.ToString(), x)))
                        : "";
                    string rqLine = reqIds.Length > 0
                        ? $"<span class=\"rq\" title=\"{HtmlEncode(reqIds)}\">{HtmlEncode(reqIds)}</span>"
                        : "";
                    sb.AppendFormat("<div class=\"{0}\" data-d=\"{1}\" data-txt=\"{2}\"><span class=\"ord\">第{3}难</span><span class=\"no\">#{4}</span><span class=\"nm\">{5}</span><span class=\"tp\">{6}</span>{7}</div>\n",
                        cls, a.IsComplete ? "1" : "0", HtmlEncode(nm), seq, id, HtmlEncode(nm), HtmlEncode(tp), rqLine);
                }
                sb.AppendLine("</div></section>");

                if (internalAch.Count > 0)
                {
                    sb.AppendLine("<section class=\"card\"><details><summary>内部追踪成就（" + internalAch.Count + " 个，非 Steam 成就）</summary><div class=\"inner\">");
                    sb.AppendLine("<table class=\"tbl\"><tr><th>ID</th><th>类型</th><th>需求数</th><th>状态</th><th>已计数单位/条目</th></tr>");
                    foreach (var a in internalAch)
                    {
                        string cls = a.IsComplete ? "done" : "miss";
                        string st = a.IsComplete ? "<span style=\"color:var(--green)\">已达成</span>" : "<span style=\"color:var(--red)\">未达成</span>";
                        string ids = a.CompleteRequirementList.Count > 0
                            ? string.Join(", ", a.CompleteRequirementList.Select(x => ResolveRequirement(a.Config.RequirementType.ToString(), x)))
                            : "-";
                        sb.AppendFormat("<tr><td>{0}</td><td>{1}</td><td>{2}</td><td>{3}</td><td class=\"ids\">{4}</td></tr>\n",
                            a.Config.AchievementId, HtmlEncode(Describe(a.Config.RequirementType.ToString())),
                            a.Config.RequirementCount, st, HtmlEncode(ids));
                    }
                    sb.AppendLine("</table></div></details></section>");
                }
            }

            sb.AppendLine("<section class=\"card\"><h2>影神图 / 博物馆</h2><div class=\"kv\">");
            sb.AppendLine(museum == null
                ? "<div><div class=\"k\">数据</div><div class=\"v\">无</div></div>"
                : $"<div><div class=\"k\">动画 (Mv)</div><div class=\"v\">{museum.MvIdList.Count}</div></div><div><div class=\"k\">配乐</div><div class=\"v\">{museum.SoundtrackIdList.Count}</div></div>");
            sb.AppendFormat("<div><div class=\"k\">图鉴妖兽</div><div class=\"v\">{0}</div></div>\n", collection?.MonsterCollectionList.Count ?? 0);
            sb.AppendLine("</div></section>");
            sb.AppendLine("</main>");

            sb.AppendLine("<footer>由 BmwAchievementViewer 生成 · 存档：" + HtmlEncode(Path.GetFileName(r.SavePath)) + "</footer>");
            sb.AppendLine("<script>");
            sb.AppendLine("(function(){");
            sb.AppendLine("var chips=document.querySelectorAll('.chip');var grid=document.getElementById('grid');var q=document.getElementById('q');");
            sb.AppendLine("function apply(){var f=document.querySelector('.chip.active').dataset.f;var t=(q.value||'').toLowerCase();");
            sb.AppendLine("grid.querySelectorAll('.ach').forEach(function(el){var ok=(f==='all')||(f==='done'&&el.dataset.d==='1')||(f==='miss'&&el.dataset.d==='0');");
            sb.AppendLine("if(ok&&t){ok=el.dataset.txt.indexOf(t)>=0;}el.classList.toggle('hide',!ok);});}");
            sb.AppendLine("chips.forEach(function(c){c.addEventListener('click',function(){chips.forEach(function(x){x.classList.remove('active')});c.classList.add('active');apply();});});");
            sb.AppendLine("if(q){q.addEventListener('input',apply);}");
            sb.AppendLine("})();");
            sb.AppendLine("</script></body></html>");
            File.WriteAllText(outPath, sb.ToString(), System.Text.Encoding.UTF8);
        }

        private static string HtmlEncode(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        private static readonly Dictionary<int, string> AchievementNames = new Dictionary<int, string>()
        {
            { 81001, "下降尘凡" },
            { 81002, "敲敲打打" },
            { 81003, "山中斗狼" },
            { 81004, "吸存运用" },
            { 81005, "真个壮怀" },
            { 81006, "长蛇隐迹" },
            { 81007, "得心应手" },
            { 81008, "余韵远传" },
            { 81009, "禅院逢友" },
            { 81010, "黑熊烧山" },
            { 81011, "捘些泥丸" },
            { 81012, "老老小小" },
            { 81013, "尺木为牢" },
            { 81014, "石中有声" },
            { 81015, "不济于谷" },
            { 81016, "击石取钥" },
            { 81017, "六字显真" },
            { 81018, "沙尘无量" },
            { 81019, "黄金引路" },
            { 81020, "父父子子" },
            { 81021, "沙海平浪" },
            { 81022, "金丹等闲" },
            { 81023, "好大风呵" },
            { 81024, "千里报国" },
            { 81025, "静息妙音" },
            { 81026, "亢宿应劫" },
            { 81027, "苦海成冰" },
            { 81028, "龟蛇盘结" },
            { 81029, "画里乾坤" },
            { 81030, "捶打神功" },
            { 81031, "铁刀高架" },
            { 81032, "新种新苗" },
            { 81033, "别有洞天" },
            { 81034, "有情众生" },
            { 81035, "魔将神归" },
            { 81036, "四大弟子" },
            { 81037, "三打马猴" },
            { 81038, "胡说胡说" },
            { 81039, "脸上有泥" },
            { 81040, "咬牙恨齿" },
            { 81041, "情深不寿" },
            { 81042, "堕龙化纹" },
            { 81043, "缫丝为线" },
            { 81044, "歪门邪道" },
            { 81045, "齐齐整整" },
            { 81046, "昂首绝唱" },
            { 81047, "巧线死结" },
            { 81048, "开眼闭情" },
            { 81049, "云游有伴" },
            { 81050, "守炉道人" },
            { 81051, "壮志未酬" },
            { 81052, "种子齐备" },
            { 81053, "草木有灵" },
            { 81054, "甘心救主" },
            { 81055, "入定蒲团" },
            { 81056, "两双一对" },
            { 81057, "大妖尽伏" },
            { 81058, "冰来火往" },
            { 81059, "无火无经" },
            { 81060, "琳琅满目" },
            { 81061, "折梅见赠" },
            { 81062, "棋逢对手" },
            { 81063, "十全十美" },
            { 81064, "美禄千钟" },
            { 81065, "云中脱险" },
            { 81066, "蛙声一片" },
            { 81067, "般般件件" },
            { 81068, "五蕴结丹" },
            { 81069, "当饭吃哩" },
            { 81070, "熟门熟路" },
            { 81071, "物各有主" },
            { 81072, "万相归真" },
            { 81073, "半个不少" },
            { 81074, "六根齐聚" },
            { 81075, "法性颇通" },
            { 81076, "收了葫芦" },
            { 81077, "心有秘方" },
            { 81078, "饮食周全" },
            { 81079, "衣冠隆盛" },
            { 81080, "夹枪带棒" },
            { 81081, "全始全终" },
        };

        private static readonly Dictionary<string, string> TypeNames = new Dictionary<string, string>()
        {
            { "NoProgressPassPrologue", "通过序章" },
            { "NoProgressFirstBuildWeapon", "首次铸造武器" },
            { "NoProgressFirstBuildArmor", "首次铸造套装" },
            { "NoProgressFirstUpgradeArmor", "首次强化装备" },
            { "NoProgressFirstSetWinePartner", "首次设置酒品" },
            { "NoProgressFirstUnlockTalent", "首次解锁神通" },
            { "NoProgressFirstUseSoulBottleGainItem", "首次使用仙体获得道具" },
            { "NoProgressFirstAlchemy", "首次炼丹" },
            { "NoProgressFirstGainGardenAward", "首次获得花园收获" },
            { "ProgressKillUnit", "击杀单位" },
            { "ProgressKillGuid", "击杀特定单位" },
            { "ProgressGainItem", "获得道具" },
            { "ProgressGainEquip", "获得装备" },
            { "ProgressGainWine", "获得酒品" },
            { "ProgressGainSoulSkill", "获得仙术" },
            { "ProgressGainSpell", "获得法术" },
            { "ProgressGainLegacy", "获得传承" },
            { "ProgressGainAllAttritem", "获得全部道具" },
            { "ProgressEnterMap", "进入地图" },
            { "ProgressUnlockMeditation", "解锁修行" },
            { "ProgressUnlockCard", "解锁图鉴" },
            { "ProgressActivateTaskStage", "推进任务阶段" },
            { "ProgressFinishTaskStage", "完成任务阶段" },
            { "ProgressAchievementComplete", "达成成就" },
        };

        private static string Describe(string type)
        {
            return TypeNames.TryGetValue(type, out string name) ? name : type;
        }

        private static readonly Dictionary<int, string> BossNames = new Dictionary<int, string>()
        {
            { 102501, "灵虚子" },
            { 102801, "白衣秀士" },
            { 102301, "金池长老" },
            { 103101, "黑熊精" },
            { 201201, "石先锋" },
            { 220501, "石敢当" },
            { 390101, "赤髯龙" },
            { 302501, "小骊龙" },
            { 202201, "虎先锋" },
            { 200101, "虎先锋" },
            { 210201, "疯虎" },
            { 600101, "蝜蝂" },
            { 201101, "黄风大圣" },
            { 301901, "魔将·妙音" },
            { 340101, "亢金星君" },
            { 302901, "青背龙" },
            { 301201, "不白" },
            { 102101, "不能" },
            { 302801, "不净" },
            { 300501, "不空" },
            { 380001, "黄眉" },
            { 510001, "猪八戒" },
            { 500801, "紫珠儿" },
            { 301601, "小黄龙" },
            { 470601, "晦月魔君" },
            { 499902, "毒敌大王" },
            { 202501, "沙国王·沙二郎" },
            { 201401, "沙大郎" },
            { 500501, "百眼魔君" },
            { 440601, "火焰山土地" },
            { 411001, "碧水金睛獸" },
            { 440501, "紅孩兒" },
            { 199901, "四大天王" },
            { 700102, "大聖殘軀" },
        };

        private static readonly Dictionary<int, string> MapNames = new Dictionary<int, string>()
        {
            { 11, "隐·观音禅院" },
            { 25, "隐·斯哈里国" },
            { 31, "如意画轴(画中)" },
            { 80, "紫云山" },
            { 70, "碧水洞" },
            //{ 62, "" },
        };

        private static string ResolveRequirement(string reqType, int id)
        {
            if (reqType == "ProgressAchievementComplete")
            {
                if (AchievementNames.TryGetValue(id, out string an)) return $"{an}";
                return $"{id}(内部成就)";
            }
            if (reqType == "ProgressKillUnit" || reqType == "ProgressKillGuid")
            {
                if (BossNames.TryGetValue(id, out string bn)) return $"{bn}";
                return id.ToString();
            }
            if (reqType == "ProgressEnterMap")
            {
                if (MapNames.TryGetValue(id, out string mn)) return $"{id}({mn})";
                return id.ToString();
            }
            return id.ToString();
        }
    }
}