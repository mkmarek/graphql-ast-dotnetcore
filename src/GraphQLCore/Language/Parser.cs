﻿namespace GraphQLCore.Language
{
    using GraphQLCore.Language.AST;

    public class Parser
    {
        private ILexer lexer;

        public Parser(ILexer lexer)
        {
            this.lexer = lexer;
        }

        public GraphQLDocument Parse(ISource source)
        {
            using (var context = new ParserContext(source, this.lexer))
            {
                return context.Parse();
            }
        }

        public GraphQLValue ParseValue(ISource source)
        {
            using (var context = new ParserContext(source, this.lexer))
            {
                return context.ParseValueLiteral(false);
            }
        }
    }
}