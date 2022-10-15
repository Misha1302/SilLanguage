﻿namespace RussianLanguage.FrontEnd.Lexer;

public static class LexerFixTokens
{
    private static readonly List<string> _usingNamespaces = new();

    public static void FixTokens(List<Token> tokens)
    {
        SetUsingNamespaces(tokens);
        SetMethods(tokens);
        SetClassesNamespaces(tokens);
        SetVariables(tokens);
        SetExpressions(tokens);
    }

    private static void SetMethods(IList<Token> tokens)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].TokenKind != Kind.MethodSeparator) continue;
            var token = tokens[++i];

            token.TokenKind = Kind.Method;
            while (tokens[i + 1].TokenKind != Kind.OpenParenthesis)
            {
                token.Text += tokens[i + 1].Text;
                tokens.RemoveAt(i + 1);
            }
        }
    }

    private static void SetClassesNamespaces(IList<Token> tokens)
    {
        for (var i = 0; i < tokens.Count; i++)
            if (tokens[i].TokenKind == Kind.Call)
            {
                i++;
                var localI = i;
                foreach (var @class in from usingNamespace in _usingNamespaces
                         select GetClassesOrValueTypes(GetFullName(usingNamespace, localI)).ToArray()
                         into classes
                         where classes.Length > 0
                         select classes.First())
                {
                    var token = tokens[localI].TokenKind != Kind.MethodSeparator
                        ? new Token(Kind.Unknown, @class.Split('.')[0] + '.')
                        : new Token(Kind.Unknown, @class);
                    tokens.Insert(i, token);

                    break;
                }
            }

        string GetFullName(string usingNamespace, int i)
        {
            return
                $"{usingNamespace}.{(tokens[i].TokenKind == Kind.MethodSeparator ? tokens[i + 1].Text : tokens[i].Text)}";
        }
    }

    private static IEnumerable<string> GetClassesOrValueTypes(string fullName)
    {
        var s = (from assembly in AppDomain.CurrentDomain.GetAssemblies()
            from type in assembly.GetTypes()
            where NamespaceHaveClassOrValueType(type)
            select type).ToArray();

        var classes = s.Where(type => type.FullName == fullName).Select(type => type.FullName!);

        var methods = from q in s
            from type in q.GetMethods()
            where $"{q.FullName}.{type.Name}" == fullName
            select q.FullName!;

        return classes.Concat(methods);
    }


    private static void SetUsingNamespaces(IReadOnlyList<Token> tokens)
    {
        for (var i = 0; i < tokens.Count; i++)
            if (tokens[i].TokenKind == Kind.Using)
                _usingNamespaces.Add(tokens[++i].Value!.ToString()!);
    }

    private static bool NamespaceHaveClassOrValueType(Type type)
    {
        return type.IsPublic && (type.IsClass || type.IsValueType);
    }

    private static void SetExpressions(List<Token> tokens)
    {
        SetIsPartOfExpression(tokens);
        ChangeExpressionToReversePolishNotation(tokens);
    }

    private static void ChangeExpressionToReversePolishNotation(List<Token> tokens)
    {
        foreach (var expressionPosition in GetExpressionsPositions(tokens))
        {
            var t = new Token[expressionPosition.count];
            tokens.GetRange(expressionPosition.startPosition, expressionPosition.count).CopyTo(t);
            var range = Rpn.GetReversePolishNotation(t.ToList());

            tokens.RemoveRange(expressionPosition.startPosition, expressionPosition.count);

            tokens.InsertRange(expressionPosition.startPosition, range);
        }
    }

    public static IEnumerable<(int startPosition, int count)> GetExpressionsPositions(IList<Token> tokens)
    {
        var offset = 0;
        for (var i = 0; i < tokens.Count; i++)
            if (tokens[i].IsPartOfExpression)
            {
                (int startPosition, int count) currentExpressionPosition = (i, -1);
                while (i < tokens.Count && tokens[i].IsPartOfExpression)
                {
                    if (tokens[i].TokenKind is Kind.OpenParenthesis or Kind.CloseParenthesis) offset++;
                    i++;
                }

                currentExpressionPosition.count = i - currentExpressionPosition.startPosition;
                i -= offset;
                offset = 0;
                yield return currentExpressionPosition;
            }
    }

    private static void SetIsPartOfExpression(IList<Token> tokens)
    {
        for (var i = 0; i < tokens.Count; i++)
            if (tokens[i].TokenKind == Kind.OpenBracket)
            {
                tokens.RemoveAt(i);
                while (tokens[i].TokenKind != Kind.CloseBracket)
                {
                    tokens[i].IsPartOfExpression = true;
                    i++;
                }

                tokens.RemoveAt(i);
            }
    }

#pragma warning disable CS8509
    private static void SetVariables(IReadOnlyList<Token> tokens)
    {
        var variables = new Dictionary<string, DataType>();
        for (var i = 0; i < tokens.Count - 2; i++)
            if (IsType(tokens[i]) && tokens[i + 2].TokenKind == Kind.AssignmentSign)
            {
                tokens[i + 1].TokenKind = Kind.CreatedVariable;
                tokens[i + 1].Type = tokens[i].TokenKind switch
                {
                    Kind.StringType => DataType.@string,
                    Kind.IntType => DataType.int32,
                    Kind.FloatType => DataType.float32,
                    Kind.BoolType => DataType.@bool
                };

                variables.Add(tokens[i + 1].Text, tokens[i + 1].Type);
                i++;
            }


        for (var index = 1; index < tokens.Count; index++)
            if (tokens[index].TokenKind != Kind.CreatedVariable && variables.ContainsKey(tokens[index].Text) &&
                tokens[index].TokenKind == Kind.Unknown)
            {
                tokens[index].TokenKind = Kind.Variable;
                tokens[index].Type = variables[tokens[index].Text];
            }
    }
#pragma warning restore CS8509

    private static bool IsType(Token token)
    {
        return token.TokenKind is Kind.StringType or Kind.FloatType or Kind.IntType or Kind.BoolType;
    }
}