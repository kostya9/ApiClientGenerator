using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

// I do not know why, but VS preview needs this
namespace System.Runtime.CompilerServices
{
    public class IsExternalInit { }
}

namespace ApiClientGenerator
{
    public record Parameter(string FullTypeName);

    public record ParameterMapping(string Key, Parameter Parameter);

    public record ActionRoute(string Name, HttpMethod Method, string Route, string? ReturnTypeName, ParameterMapping[] Mapping, ParameterMapping? Body);

    public record ControllerRoute(string Name, string BaseRoute, ActionRoute[] Actions);

    public static class Scanner
    {
        public static IEnumerable<ControllerRoute> ScanForControllers(SemanticModel semantic)
        {
            var controllerBase = semantic.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.ControllerBase");

            if (controllerBase == null)
            {
                yield break;
            }

            var allNodes = semantic.SyntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>();
            foreach (var node in allNodes)
            {
                var classSymbol = semantic.GetDeclaredSymbol(node) as INamedTypeSymbol;
                if (classSymbol != null && InheritsFrom(classSymbol, controllerBase))
                {
                    yield return ToRoute(classSymbol);
                }
            }
        }

        private static ControllerRoute ToRoute(INamedTypeSymbol classSymbol)
        {
            const string suffix = "Controller";
            var name = classSymbol.Name.EndsWith(suffix)
                ? classSymbol.Name.Substring(0, classSymbol.Name.Length - suffix.Length)
                : classSymbol.Name;

            var actionMethods = ScanForActionMethods(classSymbol)
                .ToArray();

            // Extract the route from the HttpActionAttribute
            var attribute = FindAttribute(classSymbol, a => a.ToString() == "Microsoft.AspNetCore.Mvc.RouteAttribute");
            var route = attribute?.ConstructorArguments.FirstOrDefault().Value?.ToString() ?? string.Empty;

            return new ControllerRoute(name, route, actionMethods);
        }

        private static IEnumerable<ActionRoute> ScanForActionMethods(INamedTypeSymbol classSymbol)
        {
            foreach (var member in classSymbol.GetMembers())
            {
                if (member
                    is IMethodSymbol { DeclaredAccessibility: Accessibility.Public, IsAbstract: false } symbol
                    and not { MethodKind: MethodKind.Constructor })
                {
                    var name = symbol.Name;
                    var returnType = symbol.ReturnType;

                    // Unwrap Task<T>
                    if (returnType is INamedTypeSymbol taskType
                        && taskType.OriginalDefinition.ToString() == "System.Threading.Tasks.Task<TResult>")
                    {
                        returnType = taskType.TypeArguments.First();
                    }

                    // Take unwrapped T and check whether we need to 
                    // unwrap further to V when T = ActionResult<V>
                    if (returnType is INamedTypeSymbol actionResultType
                        && actionResultType.OriginalDefinition.ToString() == "Microsoft.AspNetCore.Mvc.ActionResult<TValue>")
                    {
                        returnType = actionResultType.TypeArguments.First();
                    }

                    // If the return type is simple IActionResult -- assume that the return type is essentially void
                    if (returnType.OriginalDefinition.ToString() == "Microsoft.AspNetCore.Mvc.IActionResult")
                    {
                        returnType = null;
                    }

                    // Extract the route from the HttpActionAttribute
                    var attribute = FindAttribute(symbol, a => a.BaseType?.ToString() == "Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute");
                    var route = attribute?.ConstructorArguments.FirstOrDefault().Value?.ToString() ?? string.Empty;
                    var method = attribute?.AttributeClass?.Name switch
                    {
                        "HttpGetAttribute" => HttpMethod.Get,
                        "HttpPutAttribute" => HttpMethod.Put,
                        "HttpPostAttribute" => HttpMethod.Post,
                        "HttpDeleteAttribute" => HttpMethod.Delete,
                        _ => throw new InvalidOperationException($"Unknown attribute {attribute?.AttributeClass?.Name}")
                    };

                    var parameters = symbol.Parameters.Where(t => t.Type != null)
                        .Select(t => new ParameterMapping(t.Name, new Parameter(t.Type.ToString())))
                        .ToArray();
                    var bodyParameter = symbol.Parameters.Where(t => t.Type != null && !IsPrimitive(t.Type))
                        .Select(t => new ParameterMapping(t.Name, new Parameter(t.Type.ToString())))
                        .FirstOrDefault();

                    yield return new ActionRoute(name, method, route, returnType?.ToDisplayString(), parameters, bodyParameter);
                }
            }
        }

        private static bool InheritsFrom(INamedTypeSymbol classDeclaration, INamedTypeSymbol targetBaseType)
        {
            var currentDeclared = classDeclaration;
            while (currentDeclared.BaseType != null)
            {
                var currentBaseType = currentDeclared.BaseType;
                if (currentBaseType.Equals(targetBaseType, SymbolEqualityComparer.Default))
                {
                    return true;
                }

                currentDeclared = currentDeclared.BaseType;
            }

            return false;
        }

        private static bool IsPrimitive(ITypeSymbol typeSymbol)
        {
            switch(typeSymbol.SpecialType)
            {
                case SpecialType.System_Boolean:
                case SpecialType.System_SByte:
                case SpecialType.System_Int16:
                case SpecialType.System_Int32:
                case SpecialType.System_Int64:
                case SpecialType.System_Byte:
                case SpecialType.System_UInt16:
                case SpecialType.System_UInt32:
                case SpecialType.System_UInt64:
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                case SpecialType.System_Char:
                case SpecialType.System_String:
                    return true;
            }

            switch (typeSymbol.TypeKind)
            {
                case TypeKind.Enum:
                    return true;
            }

            return false;
        }

        private static AttributeData? FindAttribute(ISymbol symbol, Func<INamedTypeSymbol, bool> selectAttribute)
            => symbol
                .GetAttributes()
                .Where(a => a?.AttributeClass != null && selectAttribute(a.AttributeClass))
                .LastOrDefault();
    }
}
