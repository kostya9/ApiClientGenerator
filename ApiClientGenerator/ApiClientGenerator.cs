﻿using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;

namespace ApiClientGenerator
{
    [Generator]
    public class ApiClientGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            var source = new SourceStringBuilder();

            var compilation = context.Compilation;

            source.AppendLine("using System.Net.Http;");
            source.AppendLine("using System.Threading.Tasks;");
            source.AppendLine("using System.Net.Http.Json;");

            var controllerRoutes = compilation.SyntaxTrees
                .Select(t => compilation.GetSemanticModel(t))
                .Select(Scanner.ScanForControllers)
                .SelectMany(c => c)
                .ToArray();

            SetUpApiClient(controllerRoutes, source, compilation);

            foreach (var route in controllerRoutes)
            {
                SetUpSingleApi(route, source, compilation);
            }

            context.AddSource("GeneratedApiClient", source.ToString());
        }

        private void SetUpSingleApi(ControllerRoute route, SourceStringBuilder source, Compilation compilation)
        {
            source.AppendLine();
            source.AppendLine($"public class {route.Name}");
            source.AppendOpenCurlyBracketLine();

            source.AppendLine("private readonly HttpClient _client;");

            source.AppendLine();
            source.AppendLine($"public {route.Name}(HttpClient client)");

            source.AppendOpenCurlyBracketLine();
            source.AppendLine("_client = client;");
            source.AppendCloseCurlyBracketLine();

            foreach (var action in route.Actions)
            {
                var returnType = action.ReturnTypeName != null
                    ? $"Task<{action.ReturnTypeName}>"
                    : "Task";
                var parameterList = string.Join(", ", action.Mapping.Select(m => $"{m.Parameter.FullTypeName} {m.Key}"));

                source.AppendLine();
                source.AppendLine($"public async {returnType} {action.Name}({parameterList})");
                source.AppendOpenCurlyBracketLine();
                var routeValue = Path.Combine(route.BaseRoute, action.Route).Replace("\\", "/");
                var routeString = $"$\"{routeValue}\"";

                var methodString = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(action.Method.Method.ToLower());

                var callStatement = action.Body switch
                {
                    { Key: var key } => $"await _client.{methodString}AsJsonAsync({routeString}, {key});",
                    _ => $"await _client.{methodString}Async({routeString});"
                };

                if(action.ReturnTypeName == null)
                {
                    source.AppendLine(callStatement);
                }
                else
                {
                    source.AppendLine($"var result = {callStatement}");
                    source.AppendLine($"return await result.Content.ReadFromJsonAsync<{action.ReturnTypeName}>();");
                }
                
                source.AppendCloseCurlyBracketLine();
            }

            source.AppendCloseCurlyBracketLine();
        }

        private static void SetUpApiClient(IEnumerable<ControllerRoute> routes, SourceStringBuilder source, Compilation compilation)
        {
            source.AppendLine();
            source.AppendLine("public class ApiClient");
            source.AppendOpenCurlyBracketLine();

            source.AppendLine("private readonly HttpClient _client;");
            source.AppendLine();

            source.AppendLine("public ApiClient(HttpClient client)");
            source.AppendOpenCurlyBracketLine();

            source.AppendLine("_client = client;");
            
            foreach (var route in routes)
            {
                source.AppendLine($"{route.Name} = new {route.Name}(client);");
            }

            source.AppendCloseCurlyBracketLine();

            foreach (var route in routes)
            {
                source.AppendLine();
                source.AppendLine($"public {route.Name} {route.Name} {{ get; }}");
            }

            source.AppendCloseCurlyBracketLine();

        }

        public void Initialize(GeneratorInitializationContext context)
        {
            //
        }
    }
}
