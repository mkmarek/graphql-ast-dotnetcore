﻿using System.Collections.Generic;

namespace GraphQLCore.Language.AST
{
    public class GraphQLSelectionSet : ASTNode
    {
        public override ASTNodeKind Kind
        {
            get
            {
                return ASTNodeKind.SelectionSet;
            }
        }

        public IEnumerable<ASTNode> Selections { get; set; }
    }
}