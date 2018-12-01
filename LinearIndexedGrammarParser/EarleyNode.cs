using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace LinearIndexedGrammarParser
{
    public class EarleyNode : IEquatable<EarleyNode>
    {
        private const int ScanRuleNumber = 0;

        public EarleyNode(string nodeName, int startIndex, int endIndex)
        {
            Name = nodeName;
            StartIndex = startIndex;
            EndIndex = endIndex;
            RuleNumber = ScanRuleNumber;
        }


        public string Name { get; }

        public int StartIndex { get; }

        public int EndIndex { get; }

        public List<EarleyNode> Children { get; set; }

        public string AssociatedTerminal { get; set; }

        [JsonIgnore] public int RuleNumber { get; set; }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 23) ^ Name.GetHashCode();
                hash = (hash * 23) ^ StartIndex;
                hash = (hash * 23) ^ EndIndex;
                return hash;
            }
        }

        public bool HasChildren()
        {
            return Children != null;
        }

        public void AddChildren(EarleyNode v, EarleyNode w = null)
        {
            if (Children == null)
                Children = new List<EarleyNode>();
            Children.Add(v);
            if (w != null) Children.Insert(0, w);
        }

        public override string ToString()
        {
            return $"{AssociatedTerminal ?? ""} {Name} [{StartIndex}-{EndIndex}] -{RuleNumber}-";
        }

        public void Print(int level = 0)
        {
            Console.WriteLine(ToString().PadLeft(level * 4, '_'));
            if (Children == null) return;
            foreach (var child in Children)
                child.Print(level + 1);
        }

        public string TreeString(int level = 0)
        {
            var builder = new StringBuilder();
            var s = ToString();
            var s1 = s.PadLeft(level * 4 + s.Length, '_');

            builder.AppendLine(s1);
            if (Children != null)
                foreach (var child in Children)
                    builder.Append(child.TreeString(level + 1));
            return builder.ToString();
        }

        public string GetNonTerminalStringUnderNode()
        {
            var leaves = new List<string>();
            GetNonTerminalStringUnderNode(leaves);
            return string.Join(" ", leaves);
        }


        private void GetNonTerminalStringUnderNode(List<string> leavesList)
        {
            if (Children == null)
            {
                //if (Name != null && AssociatedTerminal != null)
                //    leavesList.Add(AssociatedTerminal);
                if (Name != null && AssociatedTerminal != null)
                    leavesList.Add(Name);
            }
            else
            {
                foreach (var child in Children)
                    child.GetNonTerminalStringUnderNode(leavesList);
            }
        }

        public bool Equals(EarleyNode other)
        {
            return Name == other.Name && StartIndex == other.StartIndex && EndIndex == other.EndIndex;
        }
    }
}