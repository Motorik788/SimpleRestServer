using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rest
{
    static class UriTemplateParser
    {
        static void AddChildNodeRecursive(int depth, UriTreeNode node, UriTreeNode currentNode)
        {
            if (depth > 1)
            {
                AddChildNodeRecursive(depth - 1, node, currentNode.treeNodesChild[0]);
            }
            else
            {
                currentNode.treeNodesChild.Add(node);
            }
        }


        public static UriTree Parse(string uriTemplate, HttpMethod httpMethod = HttpMethod.Get, System.Reflection.MethodInfo methodInfo = null)
        {
            UriTree res = new UriTree();
            var nodesStr = uriTemplate.Contains("?")
                ? uriTemplate.Remove(uriTemplate.IndexOf('?')).Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                : uriTemplate.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var firstNode = new UriTreeNode() { Name = nodesStr[0], HttpMethod = httpMethod };
            if (nodesStr.Length == 1)
                firstNode.Method = methodInfo;
            res.treeNodes.Add(firstNode);
            for (int i = 1; i < nodesStr.Length; i++)
            {
                UriTreeNode node = new UriTreeNode();
                if (nodesStr[i][0] == '{' && nodesStr[i][nodesStr[i].Length - 1] == '}')
                {
                    node.IsParam = true;
                    node.Name = nodesStr[i].Substring(1, nodesStr[i].Length - 2);
                }
                else
                    node.Name = nodesStr[i];

                AddChildNodeRecursive(i, node, res.treeNodes[0]);
                //метод должен иметь лишь последний элемент в дереве по определенному пути
                if (i == nodesStr.Length - 1)
                {
                    node.HttpMethod = httpMethod;
                    node.Method = methodInfo;
                }
            }

            return res;
        }
    }
}
