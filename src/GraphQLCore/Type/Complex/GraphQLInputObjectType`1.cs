namespace GraphQLCore.Type
{
    using Complex;
    using Exceptions;
    using Language.AST;
    using System;
    using System.Collections;
    using System.Linq;
    using System.Linq.Expressions;
    using Translation;
    using Utils;

    public abstract class GraphQLInputObjectType<T> : GraphQLInputObjectType
        where T : class, new()
    {
        public override Type SystemType { get; protected set; }

        public GraphQLInputObjectType(string name, string description) : base(name, description)
        {
            this.SystemType = typeof(T);
        }

        public void Field<TProperty>(string fieldName, Expression<Func<T, TProperty>> accessor)
        {
            if (this.ContainsField(fieldName))
                throw new GraphQLException("Can't insert two fields with the same name.");

            var returnType = ReflectionUtilities.GetReturnValueFromLambdaExpression(accessor);

            if (this.IsInterfaceOrCollectionOfInterfaces(returnType))
                throw new GraphQLException("Can't set accessor to interface based field");

            this.Fields.Add(fieldName, GraphQLInputObjectTypeFieldInfo.CreateAccessorFieldInfo(fieldName, accessor));
        }

        public override object GetFromAst(GraphQLValue astValue, ISchemaRepository schemaRepository)
        {
            if (!(astValue is GraphQLObjectValue))
                return null;

            var objectAstValue = (GraphQLObjectValue)astValue;
            var result = new T();

            foreach (var field in this.Fields)
            {
                var astField = GetFieldFromAstObjectValue(objectAstValue, field.Key);

                if (astField == null)
                    continue;

                var value = this.GetField(astField, field.Value, schemaRepository);

                this.AssignValueToObjectField(result, field.Value, value);
            }

            return result;
        }

        private object GetField(GraphQLObjectField astField, GraphQLInputObjectTypeFieldInfo fieldInfo, ISchemaRepository schemaRepository)
        {
            object value = this.GetValueFromField(schemaRepository, fieldInfo, astField);

            switch (astField.Value.Kind)
            {
                case ASTNodeKind.Variable:
                    value = schemaRepository.VariableResolver.GetValue((GraphQLVariable)astField.Value);
                    break;
                case ASTNodeKind.ListValue:
                    value = this.GetValueFromListValue(schemaRepository, (GraphQLListValue)astField.Value, fieldInfo, value);
                    break;
            }

            return value;
        }

        private object GetValueFromListValue(ISchemaRepository schemaRepository, GraphQLListValue listValue, GraphQLInputObjectTypeFieldInfo fieldInfo, object value)
        {
            var list = (IList)ReflectionUtilities.ChangeToCollection(value, fieldInfo.SystemType);
            var astValues = listValue.Values.ToArray();

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null && astValues[i].Kind == ASTNodeKind.Variable)
                    list[i] = schemaRepository.VariableResolver.GetValue((GraphQLVariable)astValues[i]);
            }

            return list;
        }

        private static GraphQLObjectField GetFieldFromAstObjectValue(GraphQLObjectValue objectAstValue, string fieldName)
        {
            return objectAstValue.Fields.FirstOrDefault(e => e.Name.Value == fieldName);
        }

        private void AssignValueToObjectField(T result, GraphQLInputObjectTypeFieldInfo field, object value)
        {
            if (ReflectionUtilities.IsCollection(field.SystemType))
                value = ReflectionUtilities.ChangeToCollection(value, field.SystemType);

            ReflectionUtilities.MakeSetterFromLambda(field.Lambda)
                    .DynamicInvoke(result, value);
        }

        private object GetValueFromField(
            ISchemaRepository schemaRepository,
            GraphQLFieldInfo field,
            GraphQLObjectField astField)
        {
            var graphQLType = schemaRepository.GetSchemaInputTypeFor(field.SystemType);
            var value = graphQLType.GetFromAst(astField.Value, schemaRepository);

            return value;
        }

        private bool IsInterfaceOrCollectionOfInterfaces(Type type)
        {
            if (ReflectionUtilities.IsCollection(type))
                return this.IsInterfaceOrCollectionOfInterfaces(ReflectionUtilities.GetCollectionMemberType(type));

            if (ReflectionUtilities.IsInterface(type))
                return true;

            return false;
        }
    }
}