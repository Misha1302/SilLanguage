﻿using System.Reflection;
using System.Runtime.CompilerServices;
using Lexer.FrontEnd;

namespace Lexer.Lexer;

public static class LexerFixTokens
{
    private static readonly List<string> _usingNamespaces = new();
    private static readonly List<string> _assemblies = new();

    private static readonly Assembly[] _currentDomainAssemblies = Assembly.GetExecutingAssembly()
        .GetReferencedAssemblies().Select(x => Assembly.Load(x.ToString()))
        .Concat(AppDomain.CurrentDomain.GetAssemblies()).ToArray();

    public static readonly List<Method> Methods = new();

    private static readonly Type[] _types = (from assembly in _currentDomainAssemblies
        from type in assembly.GetTypes()
        where NamespaceHaveClassOrValueType(type)
        select type).ToArray();

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    internal static void FixTokens(List<Token> tokens)
    {
        SetUsingNamespaces(tokens);
        SetMethods(tokens);
        SetVariables(tokens);
        SetClassesNamespaces(tokens);
        SetExpressions(tokens);
        AddAssemblies(tokens);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    private static void AddAssemblies(IList<Token> tokens)
    {
        foreach (var assembly in _assemblies.Distinct())
            tokens.Insert(0, new Token(Kind.Extern, $".assembly extern {assembly} {{}}"));
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
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

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    private static void SetClassesNamespaces(IList<Token> tokens)
    {
        for (var i = 0; i < tokens.Count; i++)
            if (tokens[i].TokenKind == Kind.Call)
            {
                var isFromCall = i - 2 >= 0 && tokens[i - 2].TokenKind == Kind.From;

                i++;
                var localI = i;
                if (isFromCall) tokens[localI].Text = $"{GetRealTypeName(tokens[localI - 2])}";

                AppendNamespaceOrClass(tokens, localI, isFromCall, i);
            }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    private static void AppendNamespaceOrClass(IList<Token> tokens, int localI, bool isFromCall, int i)
    {
        foreach (var @class in from usingNamespace in _usingNamespaces
                 select GetClassesOrValueTypes(GetFullName(usingNamespace))
                 into classes
                 where classes.Any()
                 select classes.First())
        {
            Method method;
            var assemblyName = GetAssembliesThatHaveClass(@class).First();
            var token = new Token(Kind.Unknown, " ");

            token.Text += $"[{assemblyName}]";
            if (tokens[localI].TokenKind != Kind.MethodSeparator)
            {
                token.Text += $"{@class.Split('.')[0]}.";
                method = GetInfoAboutMethod($"{@class}{tokens[localI + 1].Text}{tokens[localI + 2].Text}");
            }
            else
            {
                token.Text += @class;
                method = GetInfoAboutMethod($"{@class}::{tokens[localI + 1].Text}");
            }

            if (!Methods.Contains(method)) Methods.Add(method);


            if (isFromCall)
            {
                token.Text += "::";
                tokens[localI] = token;
            }
            else
            {
                tokens.Insert(i, token);
            }

            _assemblies.Add(assemblyName);


            break;
        }

        string GetFullName(string usingNamespace)
        {
            return
                $"{usingNamespace}.{(tokens[localI].TokenKind == Kind.MethodSeparator && !isFromCall ? tokens[localI + 1].Text : tokens[localI].Text)}";
        }
    }

    private static Method GetInfoAboutMethod(string methodFullName)
    {
        var methods = from q in _types
            from type in q.GetMethods()
            where $"{q.FullName}::{type.Name}" == methodFullName
            select type;

        var infoAboutMethod =
            methods.Select(x => new Method(methodFullName, GetDataTypeFromType(x.ReturnType))).First();

        return infoAboutMethod;


        DataType GetDataTypeFromType(Type getType)
        {
            if (getType == typeof(string)) return DataType.@string;
            if (getType == typeof(int)) return DataType.int32;
            if (getType == typeof(float)) return DataType.float32;
            return getType == typeof(bool) ? DataType.@bool : DataType.@null;
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    private static string GetRealTypeName(Token token)
    {
        switch (token.DataType)
        {
            case DataType.int32:
                return "Int32";
            case DataType.float32:
                return "Single";
            case DataType.@string:
                return "String";
            case DataType.@bool:
                return "Boolean";
            default:
                throw new ArgumentOutOfRangeException(token.Text);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    private static IEnumerable<string> GetClassesOrValueTypes(string fullName)
    {
        var classes = _types.Where(type => type.FullName == fullName).Select(type => type.FullName!);

        var methods = from q in _types
            from type in q.GetMethods()
            where $"{q.FullName}.{type.Name}" == fullName
            select q.FullName!;

        var classesOrValueTypes = classes.Concat(methods);
        return classesOrValueTypes;
    }

    private static IEnumerable<string> GetAssembliesThatHaveClass(string fullName)
    {
        var s = from assembly in _currentDomainAssemblies
            from type in assembly.GetTypes()
            where type.IsPublic
            where type.FullName == fullName
            select assembly.GetName().Name;

        return s;
    }


    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    private static void SetUsingNamespaces(IReadOnlyList<Token> tokens)
    {
        for (var i = 0; i < tokens.Count; i++)
            if (tokens[i].TokenKind == Kind.Using)
                _usingNamespaces.Add(tokens[++i].Value!.ToString()!);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    private static bool NamespaceHaveClassOrValueType(Type type)
    {
        return type.IsPublic && (type.IsClass || type.IsValueType);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    private static void SetExpressions(List<Token> tokens)
    {
        SetIsPartOfExpression(tokens);
        ChangeExpressionToReversePolishNotation(tokens);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
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

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
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

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
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
    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    private static void SetVariables(IReadOnlyList<Token> tokens)
    {
        var variables = new Dictionary<string, DataType>();
        for (var i = 0; i < tokens.Count - 2; i++)
            if (IsType(tokens[i]) && tokens[i + 2].TokenKind == Kind.AssignmentSign)
            {
                tokens[i + 1].TokenKind = Kind.CreatedVariable;
                tokens[i + 1].DataType = tokens[i].TokenKind switch
                {
                    Kind.StringType => DataType.@string,
                    Kind.IntType => DataType.int32,
                    Kind.FloatType => DataType.float32,
                    Kind.BoolType => DataType.@bool
                };

                variables.Add(tokens[i + 1].Text, tokens[i + 1].DataType);
                i++;
            }


        for (var index = 1; index < tokens.Count; index++)
            if (tokens[index].TokenKind != Kind.CreatedVariable && variables.ContainsKey(tokens[index].Text) &&
                tokens[index].TokenKind == Kind.Unknown)
            {
                tokens[index].TokenKind = Kind.Variable;
                tokens[index].DataType = variables[tokens[index].Text];
            }
    }
#pragma warning restore CS8509

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
    private static bool IsType(Token token)
    {
        return token.TokenKind is Kind.StringType or Kind.FloatType or Kind.IntType or Kind.BoolType;
    }
}