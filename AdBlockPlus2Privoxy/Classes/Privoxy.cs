using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AdBlockPlus2Privoxy.Classes
{
    public class Privoxy
    {
        private string _actionsFile;
        private string _filtersFile;
        private Dictionary<string, string> _dict;

        public string ActionsFile
        {
            get
            {
                return _actionsFile;
            }
        }

        public string FiltersFile
        {
            get
            {
                return _filtersFile;
            }
        }

        public void GenerateFiles(AdBlockPlus abp)
        {
            _dict = new Dictionary<string, string>();
            var actionsSb = new StringBuilder();
            var filtersSb = new StringBuilder();
            var rules = abp.Rules.Where(r => !r.isComment && !notSupported(r))
                                 .Where(r => !r.IsUrlWhitelist && !string.IsNullOrWhiteSpace(r.UrlPattern) && string.IsNullOrWhiteSpace(r.ElementPattern))
                                 .ToList();
            actionsSb.AppendLine(@"{ +block{AdblockPlus} }");
            foreach (var rule in rules)
            {
                actionsSb.AppendLine(urlPattern2Regex(rule.UrlPattern));
            }
            actionsSb.AppendLine();

            rules = abp.Rules.Where(r => !r.isComment && !notSupported(r))
                     .Where(r => r.IsUrlWhitelist && string.IsNullOrWhiteSpace(r.ElementPattern)&&!r.Directives.Any(d=>d.ToLower().Contains("image")))
                     .ToList();
            actionsSb.AppendLine(@"{ -block }");
            foreach (var rule in rules)
            {
                actionsSb.AppendLine(urlPattern2Regex(rule.UrlPattern));
            }
            actionsSb.AppendLine();

            rules = abp.Rules.Where(r => !r.isComment && !notSupported(r))
         .Where(r => r.IsUrlWhitelist && string.IsNullOrWhiteSpace(r.ElementPattern) && r.Directives.Any(d => d.ToLower().Contains("image")))
         .ToList();
            actionsSb.AppendLine(@"{ -block +handle-as-image }");
            foreach (var rule in rules)
            {
                actionsSb.AppendLine(urlPattern2Regex(rule.UrlPattern));
            }
            actionsSb.AppendLine();

            var grules = abp.Rules.Where(r => !r.isComment && !notSupported(r))
                     .Where(r => !r.IsElemWhitelist && !string.IsNullOrWhiteSpace(r.ElementPattern))
                     .GroupBy(r=>r.UrlPattern)
                     .ToList();
            foreach (var g in grules)
            {
                var sb = new StringBuilder();
                foreach (var rule in g)
                {
                    var regex = elementPattern2Regex(rule.ElementPattern);
                    if (regex != null) sb.AppendLine(regex);
                }
                if (sb.Length == 0) continue;
                if (string.IsNullOrWhiteSpace(g.Key))
                {
                    actionsSb.AppendLine(@"{ +filter{AdblockPlus} }");
                    actionsSb.AppendLine(".*");
                    filtersSb.AppendLine(@"FILTER: AdblockPlus General filters");
                }
                else
                {
                    var filterName = getFilterName(g.Key);
                    actionsSb.AppendLine(@"{ +filter{"+filterName+@"} }");
                    actionsSb.AppendLine(urlPattern2Regex(g.Key));
                    filtersSb.AppendLine(@"FILTER: "+filterName+" Filters for pattern "+g.Key);
                }
                filtersSb.Append(sb);
                filtersSb.AppendLine();
            }
            actionsSb.AppendLine();

            grules = abp.Rules.Where(r => !r.isComment && !notSupported(r))
         .Where(r => r.IsElemWhitelist && !string.IsNullOrWhiteSpace(r.ElementPattern))
         .GroupBy(r => r.UrlPattern)
         .ToList();
            foreach (var g in grules)
            {
                var sb = new StringBuilder();
                foreach (var rule in g)
                {
                    var regex = elementPattern2Regex(rule.ElementPattern);
                    if (regex != null) sb.AppendLine(regex);
                }
                if (sb.Length == 0) continue;
                if (string.IsNullOrWhiteSpace(g.Key))
                {
                    actionsSb.AppendLine(@"{ -filter{AdblockPlus_w} }");
                    actionsSb.AppendLine("*");
                    filtersSb.AppendLine(@"FILTER: AdblockPlus_w General whitelist filters");
                }
                else
                {
                    var filterName = getFilterName(g.Key) + "_w";
                    actionsSb.AppendLine(@"{ -filter{" + filterName + @"} }");
                    actionsSb.AppendLine(urlPattern2Regex(g.Key));
                    filtersSb.AppendLine(@"FILTER: " + filterName + " Whitelist filters for pattern " + g.Key);
                }
                filtersSb.Append(sb);
                filtersSb.AppendLine();
            }
            actionsSb.AppendLine();

            _actionsFile = actionsSb.ToString();
            _filtersFile = filtersSb.ToString();
            //TODO: domain ABP rules
        }

        private bool notSupported(AdBlockPlus.Rule r)
        {
            // exceptions
            if (r.UrlPattern != null && r.UrlPattern.Contains("~")) return true;
            if (r.ElementPattern != null && r.ElementPattern.Contains("~")) return true;
            if (r.Directives != null && r.Directives.Any(d=>d.Contains("~"))) return true;
            // domain rules
            if (r.Directives != null && r.Directives.Any(d => d.ToLower().Contains("domain"))) return true;
            return false;
        }

        private string getFilterName(string urlPattern)
        {
            var key = Regex.Replace(urlPattern, @"[^\w]+", "");
            if (key.Length > 30) key = key.Substring(0, 30);
            string patt;
            while (_dict.TryGetValue(key, out patt) && patt!=urlPattern)
            {
                key += "X";
            }
            _dict[key] = urlPattern;
            return key;
        }

        private static readonly Regex _regCss = new Regex(@"^((?<tag>\w+)?(#(?<id>[^\s]+))?(\.(?<class>[^\s\[]+))?(?<attr>\[([^\s=\]\^\~]+)(=[""']?([^""'\[\]]+)[""']?)?\])*(\.(?<class>[^\s]+))?)$", RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);
        private static readonly Regex _regAttr = new Regex(@"(\[(?<attrname>[^\s=\]\^\~]+)(=[""']?(?<attrval>[^""'\]]+)[""']?)?\])", RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);
        private string elementPattern2Regex(string cssPattern)
        {
            //sed '/^#/!d;s/^##//g;s/^#\(.*\)\[.*\]\[.*\]*/s|<([a-zA-Z0-9]+)\\s+.*id=.?\1.*>.*<\/\\1>||g/g;s/^#\(.*\)/s|<([a-zA-Z0-9]+)\\s+.*id=.?\1.*>.*<\/\\1>||g/g;s/^\.\(.*\)/s|<([a-zA-Z0-9]+)\\s+.*class=.?\1.*>.*<\/\\1>||g/g;s/^a\[\(.*\)\]/s|<a.*\1.*>.*<\/a>||g/g;s/^\([a-zA-Z0-9]*\)\.\(.*\)\[.*\]\[.*\]*/s|<\1.*class=.?\2.*>.*<\/\1>||g/g;s/^\([a-zA-Z0-9]*\)#\(.*\):.*[:[^:]]*[^:]*/s|<\1.*id=.?\2.*>.*<\/\1>||g/g;s/^\([a-zA-Z0-9]*\)#\(.*\)/s|<\1.*id=.?\2.*>.*<\/\1>||g/g;s/^\[\([a-zA-Z]*\).=\(.*\)\]/s|\1^=\2>||g/g;s/\^/[\/\&:\?=_]/g;s/\.\([a-zA-Z0-9]\)/\\.\1/g' ${file} >> ${filterfile}

            var match = _regCss.Match(cssPattern);
            if (!match.Success)
            {
                return null;
            }

            var start = @"s@\<([a-zA-Z0-9]+)\s+[^\>]*";
            var bits = new List<string>();
            var id = match.Groups["id"].Value;
            var cls = match.Groups["class"].Value;
            if (!string.IsNullOrWhiteSpace(id)) bits.Add(@"\bid=[""']?" + Regex.Escape(id)+@"[""'\s>]");
            if (!string.IsNullOrWhiteSpace(cls)) bits.Add(@"\bclass=[""']?([^""'\>]+\s+)?" + Regex.Escape(cls) + @"[\s""'\>]");
            foreach (Capture capture in match.Groups["attr"].Captures)
            {
                var m = _regAttr.Match(capture.Value);
                if (m.Success)
                {
                    var name = m.Groups["attrname"].Value;
                    var val = m.Groups["attrval"].Value;
                    var s = @"\b" + Regex.Escape(name);
                    if (!string.IsNullOrWhiteSpace(val))
                    {
                        s += @"=[""']?" + Regex.Escape(val)+@"[""']?";
                    }
                    bits.Add(s);
                }
            }
            var end = @"(/\>|\>.*?\<\/\1\>)@@igs";
            var result = start + string.Join("|", allCombinations(bits).Select(c => "(" + string.Join(@"[^\>]*", c) + ")")) + end;
            return result;
        }

        private IEnumerable<IEnumerable<string>> allCombinations(List<string> arr)
        {
            var list = arr.Select(str => new List<string> { str }).ToList();
            var result = fillList(arr, list);
            return result.OfType<IEnumerable<string>>();
        }

        private List<List<string>> fillList(List<string> arr, List<List<string>> list)
        {
            if (list == null || list.Count == 0 || arr.Count == list.First().Count) return list;
            var result = new List<List<string>>();
            foreach (var str in arr)
            {
                foreach (var combination in list)
                {
                    if (!combination.Contains(str) && combination.Count < arr.Count)
                    {
                        var newList = new List<string>(combination);
                        newList.Add(str);
                        result.Add(newList);
                    }
                }
            }
            return fillList(arr, result);
        }

        private string urlPattern2Regex(string line)
        {
            //TODO better (url pattern + regex)
            var splits = line.Split(',');
            if (splits.Length > 1)
            {
                return string.Join("\r\n",splits.Select(s => urlPattern2Regex(s)));
            }
            // sed '/^!.*/d;1,1 d;/^@@.*/d;/\$.*/d;/#/d;s/\./\\./g;s/\?/\\?/g;s/\*/.*/g;s/(/\\(/g;s/)/\\)/g;s/\[/\\[/g;s/\]/\\]/g;s/\^/[\/\&:\?=_]/g;s/^||/\./g;s/^|/^/g;s/|$/\$/g;/|/d' ${file} >> ${actionfile}
            line = Regex.Replace(line, @"^\|\|", ".");
            var start = false;
            var end = false;
            if (line.StartsWith("|"))
            {
                line = line.Substring(1);
                start = true;
            }
            if (line.EndsWith("|"))
            {
                line = line.Substring(0, line.Length - 1);
                end = true;
            }
            if (line.StartsWith("://"))
            {
                line = line.Substring(3);
            }
            line = line.Replace("|", "");
            line = Regex.Replace(line, @"^([^\/]*?)\^|\^$", "$1/");
            if (Regex.IsMatch(line, @"^([&_\-\?,\=]|[^/\.]+\.(gif|jpg|png|html|php|swf|htm))", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture))
            {
                line = "/*" + line;
            }
            var sb = new StringBuilder();
            if (start) sb.Append("^");
            var afterSlash = false;
            foreach (var ch in line)
            {
                switch (ch)
                {
                    case '/':
                        afterSlash = true;
                        sb.Append(ch.ToString());
                        break;
                    case '^':
                        sb.Append(@"[\/\&:\?=_]");
                        break;
                    case '*':
                        sb.Append(@".*");
                        break;
                    case '[':
                    case ']':
                    case '(':
                    case ')':
                        sb.Append(Regex.Escape(ch.ToString()));
                        break;
                    default:
                        if (afterSlash)
                        {
                            sb.Append(Regex.Escape(ch.ToString()));
                        }
                        else
                        {
                            sb.Append(ch.ToString());
                        }
                        break;
                }
            }
            if (end) sb.Append("$");
            return sb.ToString();
        }
    }
}
