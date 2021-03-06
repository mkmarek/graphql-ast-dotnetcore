﻿namespace GraphQLCore.Validation
{
    using Language;
    using Language.AST;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using Type;
    using Type.Complex;
    using Type.Introspection;
    using Type.Translation;

    public class ValidationASTVisitor : GraphQLAstVisitor
    {
        private Stack<GraphQLFieldInfo> fieldStack;
        private Stack<GraphQLBaseType> typeStack;
        private GraphQLDirective directive;

        protected IGraphQLSchema Schema { get; private set; }

        public ValidationASTVisitor(IGraphQLSchema schema)
        {
            this.typeStack = new Stack<GraphQLBaseType>();
            this.fieldStack = new Stack<GraphQLFieldInfo>();

            this.Schema = schema;
            this.SchemaRepository = schema.SchemaRepository;
            this.LiteralValueValidator = new LiteralValueValidator(this.SchemaRepository);
        }

        protected LiteralValueValidator LiteralValueValidator { get; set; }
        protected ISchemaRepository SchemaRepository { get; private set; }

        public override GraphQLDirective BeginVisitDirective(GraphQLDirective directive)
        {
            this.directive = directive;

            return base.BeginVisitDirective(directive);
        }

        public override GraphQLFieldSelection BeginVisitFieldSelection(GraphQLFieldSelection selection)
        {
            var field = this.GetField(this.GetLastType(), selection.Name.Value);

            if (field != null)
            {
                this.fieldStack.Push(field);
                this.typeStack.Push(this.SchemaRepository.GetSchemaTypeFor(field.SystemType));
            }
            else
            {
                this.fieldStack.Push(null);
                this.typeStack.Push(null);
            }

            return base.BeginVisitFieldSelection(selection);
        }

        public override GraphQLInlineFragment BeginVisitInlineFragment(GraphQLInlineFragment inlineFragment)
        {
            if (inlineFragment.TypeCondition != null)
                this.typeStack.Push(this.SchemaRepository.GetSchemaOutputTypeByName(inlineFragment.TypeCondition.Name.Value));
            else
                this.typeStack.Push(this.GetLastType());

            return base.BeginVisitInlineFragment(inlineFragment);
        }

        public override GraphQLInlineFragment EndVisitInlineFragment(GraphQLInlineFragment inlineFragment)
        {
            this.typeStack.Pop();

            return base.EndVisitInlineFragment(inlineFragment);
        }

        public override GraphQLOperationDefinition BeginVisitOperationDefinition(GraphQLOperationDefinition definition)
        {
            switch (definition.Operation)
            {
                case OperationType.Query: this.typeStack.Push(this.Schema.QueryType); break;
                case OperationType.Mutation: this.typeStack.Push(this.Schema.MutationType); break;
                case OperationType.Subscription: this.typeStack.Push(this.Schema.SubscriptionType); break;
                default: throw new NotImplementedException();
            }

            definition = base.BeginVisitOperationDefinition(definition);

            return definition;
        }

        public override GraphQLDirective EndVisitDirective(GraphQLDirective directive)
        {
            this.directive = null;

            return base.EndVisitDirective(directive);
        }

        public override GraphQLFieldSelection EndVisitFieldSelection(GraphQLFieldSelection selection)
        {
            this.fieldStack.Pop();
            this.typeStack.Pop();

            return base.EndVisitFieldSelection(selection);
        }

        public override GraphQLFragmentDefinition BeginVisitFragmentDefinition(GraphQLFragmentDefinition node)
        {
            var fragmentType = this.SchemaRepository.GetSchemaOutputTypeByName(node.TypeCondition.Name.Value);

            this.typeStack.Push(fragmentType);

            return base.BeginVisitFragmentDefinition(node);
        }

        public override GraphQLFragmentDefinition EndVisitFragmentDefinition(GraphQLFragmentDefinition node)
        {
            this.typeStack.Pop();

            return base.EndVisitFragmentDefinition(node);
        }

        public override GraphQLOperationDefinition EndVisitOperationDefinition(GraphQLOperationDefinition definition)
        {
            this.typeStack.Pop();

            return base.EndVisitOperationDefinition(definition);
        }

        public GraphQLDirective GetDirective()
        {
            return this.directive;
        }

        public GraphQLFieldInfo GetLastField()
        {
            if (this.fieldStack.Count > 0)
                return this.fieldStack.Peek();

            return null;
        }

        public GraphQLBaseType GetParentType()
        {
            if (this.typeStack.Count > 1)
                return this.typeStack.Skip(1).First();

            return null;
        }

        public GraphQLBaseType GetLastType()
        {
            if (this.typeStack.Count > 0)
            {
                return this.typeStack.Peek();
            }

            return null;
        }

        public GraphQLBaseType GetUnderlyingType(GraphQLBaseType type)
        {
            if (type is GraphQLList)
                return this.GetUnderlyingType(((GraphQLList)type).MemberType);
            if (type is GraphQLNonNull)
                return this.GetUnderlyingType(((GraphQLNonNull)type).UnderlyingNullableType);

            return type;
        }

        protected GraphQLFieldInfo GetField(GraphQLBaseType type, string fieldName)
        {
            if (this.IsQueryRootType(type))
            {
                if (fieldName == "__schema")
                    return this.GetIntrospectedSchemaField();

                if (fieldName == "__type")
                    return this.GetIntrospectedTypeField();
            }

            if (type is GraphQLNonNull)
                return this.GetField(((GraphQLNonNull)type).UnderlyingNullableType, fieldName);

            if (type is GraphQLInputObjectType)
                return ((GraphQLInputObjectType)type).GetFieldInfo(fieldName);

            if (type is GraphQLComplexType)
                return ((GraphQLComplexType)type).GetFieldInfo(fieldName);

            if (type is GraphQLList)
                return this.GetField(((GraphQLList)type).MemberType, fieldName);

            return null;
        }

        protected GraphQLBaseType GetLastArgumentType(GraphQLArgument argument)
        {
            if (this.directive != null)
            {
                var directiveType = this.SchemaRepository.GetDirective(this.directive.Name.Value);
                return directiveType?.GetArgument(argument.Name.Value)?
                    .GetGraphQLType(this.SchemaRepository);
            }
            else
            {
                var field = this.GetLastField();

                if (field == null)
                {
                    return null;
                }

                return field
                    .Arguments
                    .SingleOrDefault(e => e.Key == argument.Name.Value)
                    .Value?.GetGraphQLType(this.SchemaRepository);
            }
        }

        private GraphQLFieldInfo GetIntrospectedTypeField()
        {
            return GraphQLObjectTypeFieldInfo.CreateResolverFieldInfo(
                "__type",
                (Expression<Func<string, IntrospectedType>>)((string name) => this.Schema.IntrospectType(name)),
                "Request the type information of a single type.");
        }

        private GraphQLFieldInfo GetIntrospectedSchemaField()
        {
            return GraphQLObjectTypeFieldInfo.CreateResolverFieldInfo(
                "__schema",
                (Expression<Func<IntrospectedSchemaType>>)(() => this.Schema.IntrospectedSchema),
                "Access the current type schema of this server.");
        }

        private bool IsQueryRootType(GraphQLBaseType type)
        {
            return type == this.Schema.QueryType;
        }
    }
}