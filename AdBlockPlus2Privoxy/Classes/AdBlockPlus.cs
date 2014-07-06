using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AdBlockPlus2Privoxy.Classes
{
    public class AdBlockPlus
    {
        private List<Rule> _rules;

        public AdBlockPlus()
        {
            _rules = new List<Rule>();
        }

        public void Load(string fileName)
        {
            using (var stream = File.OpenRead(fileName))
            {
                Load(stream);
            }
        }

        public void Load(Stream stream)
        {
            using (var sr = new StreamReader(stream))
            {
                Load(sr);
            }
        }

        private void Load(TextReader sr)
        {
            _rules.Clear();
            var line = sr.ReadLine();
            if (line == null || !Regex.IsMatch(line, @"^\[Adblock.*\]$"))
            {
                throw new Exception("The file is not an AdBlockPlus list.");
            }
            foreach (var newline in enumerateLines(sr))
            {
                var rule = new Rule();
                if (isComment(newline))
                {
                    rule.Comment = newline;
                    _rules.Add(rule);
                    continue;
                }
                var match = Regex.Match(newline, @"^(?<line>.*?)(?<element>#[@]?#.+)?(\$((?<directive>[^,$]+)(,(?<directive>[^,$]+))*))?$");
                if (!match.Success) continue;


                line = match.Groups["line"].Value;
                var directives = match.Groups["directive"].Captures.OfType<Capture>().Select(c => c.Value).ToList();
                var element = match.Groups["element"].Value;
                if (line.StartsWith("@@"))
                {
                    line = line.Substring(2);
                    rule.IsUrlWhitelist = true;
                }
                rule.UrlPattern = line;
                if (!string.IsNullOrWhiteSpace(element))
                {
                    if (element.StartsWith("#@#"))
                    {
                        rule.IsElemWhitelist = true;
                        element = element.Substring(3);
                    }
                    else
                    {
                        element = element.Substring(2);
                    }
                    rule.ElementPattern = element;
                }
                rule.Directives = directives;
                _rules.Add(rule);
            }
        }

        private IEnumerable<string> enumerateLines(TextReader sr)
        {
            string line;
            while ((line = sr.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                yield return line;
            }
        }

        private bool isComment(string line)
        {
            return line.StartsWith("!");
        }

        public List<Rule> Rules
        {
            get { return _rules; }
        }

        public class Rule
        {
            public bool IsUrlWhitelist { get; set; }
            public string UrlPattern { get; set; }

            public bool IsElemWhitelist { get; set; }

            public string ElementPattern { get; set; }

            public List<string> Directives { get; set; }

            public string Comment { get; set; }
            public bool isComment { get { return Comment != null; } }
        }
    }
}
