namespace GraphQLCore.Tests.Validation
{
    using GraphQLCore.Language;
    using GraphQLCore.Language.AST;
    using GraphQLCore.Type;
    using GraphQLCore.Validation;
    using NUnit.Framework;
    using Schemas;
    using System.Collections.Generic;
    using System.Linq;

    [TestFixture]
    public class VariableUsagesProviderTests
    {
        private VariableUsagesProviderTestVisitor visitor;

        [SetUp]
        public void SetUp()
        {
            var schema = new TestSchema();
            this.visitor = new VariableUsagesProviderTestVisitor(schema);
        }

        [Test]
        public void VariableUsagesProvider_WithOneVariable_ReturnsOneUsage()
        {
            var usages = visitor.GetUsages(@"
                { 
                    field (a: $var)
                }
            ");

            Assert.AreEqual(1, usages.Count());
            Assert.AreEqual("var", usages.ElementAt(0).Variable.Name.Value);
        }

        [Test]
        public void VariableUsagesProvider_WithMultipleVariables_ReturnsMultipleUsages()
        {
            var usages = visitor.GetUsages(@"
                { 
                    field (a: $var)
                    field2 : field (a: $a, b: $b, c: $c)
                }
            ");

            Assert.AreEqual(4, usages.Count());
        }

        [Test]
        public void VariableUsagesProvider_WithVariablesInArray_ReturnsCorrectUsages()
        {
            var usages = visitor.GetUsages(@"
                { 
                    sum (arg: [$a, 1, $b])
                }
            ");

            Assert.AreEqual(2, usages.Count());
            Assert.AreEqual("a", usages.ElementAt(0).Variable.Name.Value);
            Assert.AreEqual("Int", usages.ElementAt(0).ArgumentType.ToString());
            Assert.AreEqual("b", usages.ElementAt(1).Variable.Name.Value);
            Assert.AreEqual("Int", usages.ElementAt(1).ArgumentType.ToString());
        }

        [Test]
        public void VariableUsagesProvider_WithVariablesInObject_ReturnsCorrectUsages()
        {
            var usages = visitor.GetUsages(@"
                { 
                    complicatedArgs {
                        complicatedObjectArgField (complicatedObjectArg: {
                            intField: $a,
                            complicatedObjectArray: [{
                                stringField: $string,
                                enumField: $enum
                            },
                            $complicatedObject]
                        })
                    }
                }
            ");

            Assert.AreEqual(4, usages.Count());
            Assert.AreEqual("Int", usages.ElementAt(0).ArgumentType.ToString());
            Assert.AreEqual("String", usages.ElementAt(1).ArgumentType.ToString());
            Assert.AreEqual("FurColor", usages.ElementAt(2).ArgumentType.ToString());
            Assert.AreEqual("ComplicatedInputObjectType", usages.ElementAt(3).ArgumentType.ToString());
        }
    }

    public class VariableUsagesProviderTestVisitor : ValidationASTVisitor
    {
        private IGraphQLSchema schema;
        private GraphQLOperationDefinition operation;
        private Parser parser;

        public VariableUsagesProviderTestVisitor(IGraphQLSchema schema)
            : base (schema)
        {
            this.schema = schema;
            this.parser = new Parser(new Lexer());
        }

        public override GraphQLOperationDefinition EndVisitOperationDefinition(GraphQLOperationDefinition operation)
        {
            this.operation = operation;

            return base.EndVisitOperationDefinition(operation);
        }

        public IEnumerable<VariableUsage> GetUsages(string query)
        {
            var document = this.parser.Parse(new Source(query));
            this.Visit(document);

            return VariableUsagesProvider.Get(this.operation, document, this.schema);
        }
    }
}
