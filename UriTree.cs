using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rest
{
    class UriTreeNode
    {
        public string Name;
        public bool IsParam;
        public List<UriTreeNode> treeNodesChild = new List<UriTreeNode>();
        public System.Reflection.MethodInfo Method;
        public HttpMethod HttpMethod;
    }

    class UriTree
    {
        public List<UriTreeNode> treeNodes = new List<UriTreeNode>();

        public void Union(UriTree tree)
        {
            UnionRec(tree.treeNodes, treeNodes);
        }

        public void UnionChild(UriTree tree)
        {
            UnionRec(tree.treeNodes, treeNodes[0].treeNodesChild);
        }

        public UriTreeNode Find(string[] path, HttpMethod httpMethod, Dictionary<string, object> param)
        {
            var cur = treeNodes;
            int i = 0;
            UriTreeNode node = null;
            if (path.Length > 0)
            {
                while (i < path.Length)
                {
                    node = cur.Find(x => ((!x.IsParam && x.Name == path[i]) || (x.IsParam)) 
                    && (x.Method != null ? x.HttpMethod == httpMethod : x.Method == null 
                    && (x.HttpMethod == HttpMethod.none || x.HttpMethod == httpMethod)));
                    if (node != null)
                    {
                        if (node.IsParam)
                        {
                            param.Add(node.Name, path[i]);
                        }
                        i++;
                        cur = node.treeNodesChild;
                    }
                    else return null;
                }
            }

            return node;
        }

        private void UnionRec(List<UriTreeNode> treeNodes1, List<UriTreeNode> treeNodes2)
        {
            foreach (var item in treeNodes1)
            {
                var node = treeNodes2.FirstOrDefault(x => x.Name == item.Name && item.HttpMethod == x.HttpMethod);
                if (node == null)
                    treeNodes2.Add(item);
                else if (node != null && node.Method == null && item.treeNodesChild.Count == 0 && item.Method != null)
                {
                    node.Method = item.Method;
                    node.HttpMethod = item.HttpMethod;
                }
                else
                    UnionRec(item.treeNodesChild, node.treeNodesChild);
            }
        }
    }
}
