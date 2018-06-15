using System;
using System.Collections.Generic;

namespace LinearIndexedGrammarParser
{
    public class EarleyNode
    {
        private const int ScanRuleNumber = 0;

        public EarleyNode() {}

        public EarleyNode(string nodeName, int startIndex, int endIndex)
        {
            Name = nodeName;
            StartIndex = startIndex;
            EndIndex = endIndex;
            RuleNumber = ScanRuleNumber;
            LogProbability = 0.0f;
            Bits = 1;
        }

        public double LogProbability { get; set; }
        public int Bits { get; set; }
        public string Name { get; set; }
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public List<EarleyNode> Children { get; set; }
        public int RuleNumber { get; set; }
        public string AssociatedTerminal { get; set; }

        public override bool Equals(object obj)
        {
            if (!(obj is EarleyNode n))
                return false;

            return (Name == n.Name) && (StartIndex == n.StartIndex) && (EndIndex == n.EndIndex);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + Name.GetHashCode();
                hash = hash * 23 + StartIndex;
                hash = hash * 23 + EndIndex;
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
            if (w != null)
            {
                Children.Insert(0, w);
            }
        }

        public override string ToString()
        {
            return $"{AssociatedTerminal ?? ""} {Name} [{StartIndex}-{EndIndex}] [p:{LogProbability}] -{RuleNumber}-";
        }

        public void Print(int level = 0)
        {
            Console.WriteLine(ToString().PadLeft(level * 4, '_'));
            if (Children == null) return;
            foreach (var child in Children)
                child.Print(level + 1);
        }

        public string GetTerminalStringUnderNode()
        {
            var leaves = new List<string>();
            GetTerminalStringUnderNode(leaves);
            return string.Join(" ", leaves);
        }


        private void GetTerminalStringUnderNode(List<string> leavesList)
        {
            if (Children == null)
            {
                if (Name != null && AssociatedTerminal != null)
                    leavesList.Add(AssociatedTerminal);
            }
            else
            {
                foreach (var child in Children)
                    child.GetTerminalStringUnderNode(leavesList);
            }
        }
    }
}