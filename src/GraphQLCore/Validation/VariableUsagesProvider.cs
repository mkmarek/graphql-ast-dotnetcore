namespace GraphQLCore.Validation
{
    using Language.AST;
    using System.Collections.Generic;
    using Type;

    public class VariableUsagesProvider : ValidationASTVisitor
    {
        private IDictionary<string, GraphQLFragmentDefinition> fragments;
        private List<VariableUsage> variableUsages;
        private Stack<GraphQLBaseType> inputTypeStack;

        private VariableUsagesProvider(IDictionary<string, GraphQLFragmentDefinition> fragments, IGraphQLSchema schema)
            : base(schema)
        {
            this.variableUsages = new List<VariableUsage>();
            this.fragments = fragments;
            this.inputTypeStack = new Stack<GraphQLBaseType>();
        }

        public override GraphQLListValue BeginVisitListValue(GraphQLListValue node)
        {
            var lastType = this.GetLastInputType();

            if (lastType is GraphQLList)
            {
                var type = ((GraphQLList)lastType).MemberType;
                this.inputTypeStack.Push(type);

                node = base.BeginVisitListValue(node);
                this.inputTypeStack.Pop();

                return node;
            }

            return base.BeginVisitListValue(node);
        }

        public override GraphQLObjectField BeginVisitObjectField(GraphQLObjectField node)
        {
            var field = (this.GetLastField()
            ?.GetGraphQLType(this.SchemaRepository) as GraphQLComplexType)
            ?.GetFieldInfo(node.Name.Value);

            if (field != null)
            {
                var type = this.SchemaRepository.GetSchemaInputTypeFor(field.SystemType);
                this.inputTypeStack.Push(type);

                node = base.BeginVisitObjectField(node);
                this.inputTypeStack.Pop();

                return node;
            }

            return base.BeginVisitObjectField(node);
        }

        public override GraphQLArgument BeginVisitArgument(GraphQLArgument argument)
        {
            if (this.GetLastField().Arguments.ContainsKey(argument.Name.Value))
            {
                var type = this.GetLastField().Arguments[argument.Name.Value].GetGraphQLType(this.SchemaRepository);
                this.inputTypeStack.Push(type);

                argument = base.BeginVisitArgument(argument);
                this.inputTypeStack.Pop();

                return argument;
            }

            return base.BeginVisitArgument(argument);
        }

        public GraphQLBaseType GetLastInputType()
        {
            if (this.inputTypeStack.Count > 0)
            {
                var type = this.inputTypeStack.Peek();

                return type;
            }

            var fieldType = this.GetLastType() as GraphQLComplexType;

            return this.SchemaRepository.GetSchemaInputTypeFor(fieldType.SystemType);
        }

        public static IEnumerable<VariableUsage> Get(
            GraphQLOperationDefinition operation, GraphQLDocument document, IGraphQLSchema schema)
        {
            var visitor = new VariableUsagesProvider(GetFragmentsFromDocument(document), schema);

            visitor.BeginVisitOperationDefinition(operation);

            return visitor.variableUsages;
        }

        public override GraphQLFragmentSpread BeginVisitFragmentSpread(GraphQLFragmentSpread fragmentSpread)
        {
            if (this.fragments.ContainsKey(fragmentSpread.Name.Value))
            {
                var fragment = this.fragments[fragmentSpread.Name.Value];
                this.fragments.Remove(fragmentSpread.Name.Value);

                this.BeginVisitFragmentDefinition(fragment);
            }

            return base.BeginVisitFragmentSpread(fragmentSpread);
        }

        public override GraphQLVariable BeginVisitVariable(GraphQLVariable variable)
        {
            var usage = this.CreateUsage(variable);

            if (usage != null)
                this.variableUsages.Add(usage);

            return base.BeginVisitVariable(variable);
        }

        private static IDictionary<string, GraphQLFragmentDefinition> GetFragmentsFromDocument(GraphQLDocument document)
        {
            var fragments = new Dictionary<string, GraphQLFragmentDefinition>();

            foreach (var definition in document.Definitions)
            {
                if (definition.Kind == ASTNodeKind.FragmentDefinition)
                {
                    var fragment = (GraphQLFragmentDefinition)definition;
                    if (!fragments.ContainsKey(fragment.Name.Value))
                        fragments.Add(fragment.Name.Value, fragment);
                }
            }

            return fragments;
        }

        private VariableUsage CreateUsage(GraphQLVariable variable)
        {
            var type = this.GetLastInputType();
           
            return new VariableUsage()
            {
                ArgumentType = type,
                Variable = variable
            };
        }
    }
}