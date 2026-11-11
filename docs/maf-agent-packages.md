---
type: reference
title: Paquetes del agente MAF — antes y después
description: Lista de paquetes de Infrastructure antes y después de añadir Microsoft Agent Framework (T001), para vigilar cambios transitivos que afecten al agente SK.
tags: [maf, packages, sdd]
timestamp: 2026-11-11T23:50:00Z
---

# Paquetes del agente MAF — antes y después (T001)

Aprobado por el usuario el 2026-11-11: **MAF 1.28.0**, aceptando el salto transitivo de `Microsoft.Extensions.AI*` 10.5 → 10.11 y `OpenAI` 2.10 → 2.14 bajo `Microsoft.SemanticKernel` 1.81.0 (sin cambiar SK). Ninguna versión actual de MAF usa MEAI 10.5 (1.4.0 = 10.5.0, 1.8.0 = 10.5.1, 1.10–1.14 = 10.6, 1.16–1.18 = 10.7, 1.20 = 10.9, 1.22–1.26 = 10.10, 1.28 = 10.11).

T034 comprueba que estas versiones no cambian sin aprobación.

## Paquetes de IA — diferencia

```diff
0a1,2
>  > Microsoft.Agents.AI 1.28.0 1.28.0 
>  > Microsoft.Extensions.AI.OpenAI 10.11.0 10.11.0 
3,5c5,8
<  > Microsoft.Extensions.AI 10.5.0 
<  > Microsoft.Extensions.AI.Abstractions 10.5.2 
<  > Microsoft.Extensions.AI.OpenAI 10.5.0 
---
>  > Microsoft.Agents.AI.Abstractions 1.28.0 
>  > Microsoft.Extensions.AI 10.11.0 
>  > Microsoft.Extensions.AI.Abstractions 10.11.0 
>  > Microsoft.Extensions.AI.Evaluation 10.11.0 
10c13
<  > OpenAI 2.10.0 
---
>  > OpenAI 2.14.0 
```

## Antes (development + global.json)

```text
  Determinando los proyectos que se van a restaurar...
  Todos los proyectos están actualizados para la restauración.
El proyecto "Infrastructure" tiene las referencias de paquete siguientes
   [net10.0]: 
   Paquete de nivel superior                          Solicitado   Resuelto
   > AspNetCore.HealthChecks.CosmosDb                 9.0.0        9.0.0   
   > AspNetCore.HealthChecks.NpgSql                   9.0.0        9.0.0   
   > AspNetCore.HealthChecks.UI                       9.0.0        9.0.0   
   > AspNetCore.HealthChecks.UI.Client                9.0.0        9.0.0   
   > AspNetCore.HealthChecks.UI.InMemory.Storage      9.0.0        9.0.0   
   > EFCore.NamingConventions                         10.0.1       10.0.1  
   > KubernetesClient                                 21.0.8       21.0.8  
   > MailKit                                          4.18.2       4.18.2  
   > Microsoft.Azure.Cosmos                           3.63.4       3.63.4  
   > Microsoft.EntityFrameworkCore                    10.0.14      10.0.14 
   > Microsoft.EntityFrameworkCore.Design             10.0.14      10.0.14 
   > Microsoft.EntityFrameworkCore.InMemory           10.0.14      10.0.14 
   > Microsoft.Extensions.Caching.Hybrid              10.11.0      10.11.0 
   > Microsoft.Extensions.Hosting                     10.0.14      10.0.14 
   > Microsoft.Extensions.Http                        10.0.14      10.0.14 
   > Microsoft.Extensions.Http.Resilience             10.11.0      10.11.0 
   > Microsoft.SemanticKernel                         1.81.0       1.81.0  
   > Newtonsoft.Json                                  13.0.4       13.0.4  
   > Npgsql.EntityFrameworkCore.PostgreSQL            10.0.3       10.0.3  
   > Serilog                                          4.4.0        4.4.0   
   > Serilog.AspNetCore                               10.0.0       10.0.0  
   > Serilog.Enrichers.Environment                    3.0.1        3.0.1   
   > Serilog.Enrichers.Thread                         4.0.0        4.0.0   
   > Serilog.Formatting.Compact                       3.0.0        3.0.0   
   > Serilog.Sinks.Seq                                9.1.0        9.1.0   
   > System.IdentityModel.Tokens.Jwt                  8.23.0       8.23.0  
   > System.Security.Cryptography.Pkcs                10.0.14      10.0.14 

   Paquete transitivo                                                Resuelto    
   > AspNetCore.HealthChecks.UI.Core                                 9.0.0       
   > AspNetCore.HealthChecks.UI.Data                                 9.0.0       
   > Azure.AI.OpenAI                                                 2.9.0-beta.1
   > Azure.Core                                                      1.51.1      
   > BouncyCastle.Cryptography                                       2.7.0       
   > ErrorOr                                                         2.1.1       
   > FluentValidation                                                12.2.0      
   > FluentValidation.DependencyInjectionExtensions                  12.2.0      
   > Fractions                                                       8.3.2       
   > Humanizer.Core                                                  2.14.1      
   > MediatR                                                         14.3.0      
   > MediatR.Contracts                                               2.0.1       
   > Microsoft.Bcl.AsyncInterfaces                                   10.0.2      
   > Microsoft.Bcl.Cryptography                                      10.0.2      
   > Microsoft.Bcl.HashCode                                          1.1.1       
   > Microsoft.Build.Framework                                       18.0.2      
   > Microsoft.CodeAnalysis.Analyzers                                3.11.0      
   > Microsoft.CodeAnalysis.Common                                   5.0.0       
   > Microsoft.CodeAnalysis.CSharp                                   5.0.0       
   > Microsoft.CodeAnalysis.CSharp.Workspaces                        5.0.0       
   > Microsoft.CodeAnalysis.Workspaces.Common                        5.0.0       
   > Microsoft.CodeAnalysis.Workspaces.MSBuild                       5.0.0       
   > Microsoft.EntityFrameworkCore.Abstractions                      10.0.14     
   > Microsoft.EntityFrameworkCore.Analyzers                         10.0.14     
   > Microsoft.EntityFrameworkCore.Relational                        10.0.14     
   > Microsoft.Extensions.AI                                         10.5.0      
   > Microsoft.Extensions.AI.Abstractions                            10.5.2      
   > Microsoft.Extensions.AI.OpenAI                                  10.5.0      
   > Microsoft.Extensions.AmbientMetadata.Application                10.11.0     
   > Microsoft.Extensions.Caching.Abstractions                       10.0.14     
   > Microsoft.Extensions.Caching.Memory                             10.0.14     
   > Microsoft.Extensions.Compliance.Abstractions                    10.11.0     
   > Microsoft.Extensions.Configuration                              10.0.14     
   > Microsoft.Extensions.Configuration.Abstractions                 10.0.14     
   > Microsoft.Extensions.Configuration.Binder                       10.0.14     
   > Microsoft.Extensions.Configuration.CommandLine                  10.0.14     
   > Microsoft.Extensions.Configuration.EnvironmentVariables         10.0.14     
   > Microsoft.Extensions.Configuration.FileExtensions               10.0.14     
   > Microsoft.Extensions.Configuration.Json                         10.0.14     
   > Microsoft.Extensions.Configuration.UserSecrets                  10.0.14     
   > Microsoft.Extensions.DependencyInjection                        10.0.14     
   > Microsoft.Extensions.DependencyInjection.Abstractions           10.0.14     
   > Microsoft.Extensions.DependencyInjection.AutoActivation         10.11.0     
   > Microsoft.Extensions.DependencyModel                            10.0.14     
   > Microsoft.Extensions.Diagnostics                                10.0.14     
   > Microsoft.Extensions.Diagnostics.Abstractions                   10.0.14     
   > Microsoft.Extensions.Diagnostics.ExceptionSummarization         10.11.0     
   > Microsoft.Extensions.Diagnostics.HealthChecks                   8.0.11      
   > Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions      8.0.11      
   > Microsoft.Extensions.FileProviders.Abstractions                 10.0.14     
   > Microsoft.Extensions.FileProviders.Physical                     10.0.14     
   > Microsoft.Extensions.FileSystemGlobbing                         10.0.14     
   > Microsoft.Extensions.Hosting.Abstractions                       10.0.14     
   > Microsoft.Extensions.Http.Diagnostics                           10.11.0     
   > Microsoft.Extensions.Logging                                    10.0.14     
   > Microsoft.Extensions.Logging.Abstractions                       10.0.14     
   > Microsoft.Extensions.Logging.Configuration                      10.0.14     
   > Microsoft.Extensions.Logging.Console                            10.0.14     
   > Microsoft.Extensions.Logging.Debug                              10.0.14     
   > Microsoft.Extensions.Logging.EventLog                           10.0.14     
   > Microsoft.Extensions.Logging.EventSource                        10.0.14     
   > Microsoft.Extensions.ObjectPool                                 10.0.13     
   > Microsoft.Extensions.Options                                    10.0.14     
   > Microsoft.Extensions.Options.ConfigurationExtensions            10.0.14     
   > Microsoft.Extensions.Primitives                                 10.0.14     
   > Microsoft.Extensions.Resilience                                 10.11.0     
   > Microsoft.Extensions.Telemetry                                  10.11.0     
   > Microsoft.Extensions.Telemetry.Abstractions                     10.11.0     
   > Microsoft.Extensions.VectorData.Abstractions                    10.5.2      
   > Microsoft.IdentityModel.Abstractions                            8.23.0      
   > Microsoft.IdentityModel.JsonWebTokens                           8.23.0      
   > Microsoft.IdentityModel.Logging                                 8.23.0      
   > Microsoft.IdentityModel.Tokens                                  8.23.0      
   > Microsoft.SemanticKernel.Abstractions                           1.81.0      
   > Microsoft.SemanticKernel.Connectors.AzureOpenAI                 1.81.0      
   > Microsoft.SemanticKernel.Connectors.OpenAI                      1.81.0      
   > Microsoft.SemanticKernel.Core                                   1.81.0      
   > Microsoft.VisualStudio.SolutionPersistence                      1.0.52      
   > MimeKit                                                         4.18.2      
   > Mono.TextTemplating                                             3.0.0       
   > Npgsql                                                          10.0.3      
   > OpenAI                                                          2.10.0      
   > Polly.Core                                                      8.4.2       
   > Polly.Extensions                                                8.4.2       
   > Polly.RateLimiting                                              8.4.2       
   > Serilog.Extensions.Hosting                                      10.0.0      
   > Serilog.Extensions.Logging                                      10.0.0      
   > Serilog.Settings.Configuration                                  10.0.0      
   > Serilog.Sinks.Console                                           6.1.1       
   > Serilog.Sinks.Debug                                             3.0.0       
   > Serilog.Sinks.File                                              7.0.0       
   > System.ClientModel                                              1.10.0      
   > System.CodeDom                                                  6.0.0       
   > System.Composition                                              9.0.0       
   > System.Composition.AttributedModel                              9.0.0       
   > System.Composition.Convention                                   9.0.0       
   > System.Composition.Hosting                                      9.0.0       
   > System.Composition.Runtime                                      9.0.0       
   > System.Composition.TypedParts                                   9.0.0       
   > System.Configuration.ConfigurationManager                       10.0.14     
   > System.Diagnostics.EventLog                                     10.0.14     
   > System.Memory.Data                                              10.0.3      
   > System.Numerics.Tensors                                         10.0.10     
   > System.Security.Cryptography.ProtectedData                      10.0.14     
   > System.Threading.RateLimiting                                   8.0.0       
   > YamlDotNet                                                      18.0.0      

```

## Después (MAF 1.28.0 + Microsoft.Extensions.AI.OpenAI 10.11.0)

```text
  Determinando los proyectos que se van a restaurar...
  Todos los proyectos están actualizados para la restauración.
El proyecto "Infrastructure" tiene las referencias de paquete siguientes
   [net10.0]: 
   Paquete de nivel superior                          Solicitado   Resuelto
   > AspNetCore.HealthChecks.CosmosDb                 9.0.0        9.0.0   
   > AspNetCore.HealthChecks.NpgSql                   9.0.0        9.0.0   
   > AspNetCore.HealthChecks.UI                       9.0.0        9.0.0   
   > AspNetCore.HealthChecks.UI.Client                9.0.0        9.0.0   
   > AspNetCore.HealthChecks.UI.InMemory.Storage      9.0.0        9.0.0   
   > EFCore.NamingConventions                         10.0.1       10.0.1  
   > KubernetesClient                                 21.0.8       21.0.8  
   > MailKit                                          4.18.2       4.18.2  
   > Microsoft.Agents.AI                              1.28.0       1.28.0  
   > Microsoft.Azure.Cosmos                           3.63.4       3.63.4  
   > Microsoft.EntityFrameworkCore                    10.0.14      10.0.14 
   > Microsoft.EntityFrameworkCore.Design             10.0.14      10.0.14 
   > Microsoft.EntityFrameworkCore.InMemory           10.0.14      10.0.14 
   > Microsoft.Extensions.AI.OpenAI                   10.11.0      10.11.0 
   > Microsoft.Extensions.Caching.Hybrid              10.11.0      10.11.0 
   > Microsoft.Extensions.Hosting                     10.0.14      10.0.14 
   > Microsoft.Extensions.Http                        10.0.14      10.0.14 
   > Microsoft.Extensions.Http.Resilience             10.11.0      10.11.0 
   > Microsoft.SemanticKernel                         1.81.0       1.81.0  
   > Newtonsoft.Json                                  13.0.4       13.0.4  
   > Npgsql.EntityFrameworkCore.PostgreSQL            10.0.3       10.0.3  
   > Serilog                                          4.4.0        4.4.0   
   > Serilog.AspNetCore                               10.0.0       10.0.0  
   > Serilog.Enrichers.Environment                    3.0.1        3.0.1   
   > Serilog.Enrichers.Thread                         4.0.0        4.0.0   
   > Serilog.Formatting.Compact                       3.0.0        3.0.0   
   > Serilog.Sinks.Seq                                9.1.0        9.1.0   
   > System.IdentityModel.Tokens.Jwt                  8.23.0       8.23.0  
   > System.Security.Cryptography.Pkcs                10.0.14      10.0.14 

   Paquete transitivo                                                Resuelto    
   > AspNetCore.HealthChecks.UI.Core                                 9.0.0       
   > AspNetCore.HealthChecks.UI.Data                                 9.0.0       
   > Azure.AI.OpenAI                                                 2.9.0-beta.1
   > Azure.Core                                                      1.51.1      
   > BouncyCastle.Cryptography                                       2.7.0       
   > ErrorOr                                                         2.1.1       
   > FluentValidation                                                12.2.0      
   > FluentValidation.DependencyInjectionExtensions                  12.2.0      
   > Fractions                                                       8.3.2       
   > Google.Protobuf                                                 3.30.2      
   > Humanizer.Core                                                  2.14.1      
   > MediatR                                                         14.3.0      
   > MediatR.Contracts                                               2.0.1       
   > Microsoft.Agents.AI.Abstractions                                1.28.0      
   > Microsoft.Bcl.AsyncInterfaces                                   10.0.2      
   > Microsoft.Bcl.Cryptography                                      10.0.2      
   > Microsoft.Bcl.HashCode                                          1.1.1       
   > Microsoft.Build.Framework                                       18.0.2      
   > Microsoft.CodeAnalysis.Analyzers                                3.11.0      
   > Microsoft.CodeAnalysis.Common                                   5.0.0       
   > Microsoft.CodeAnalysis.CSharp                                   5.0.0       
   > Microsoft.CodeAnalysis.CSharp.Workspaces                        5.0.0       
   > Microsoft.CodeAnalysis.Workspaces.Common                        5.0.0       
   > Microsoft.CodeAnalysis.Workspaces.MSBuild                       5.0.0       
   > Microsoft.EntityFrameworkCore.Abstractions                      10.0.14     
   > Microsoft.EntityFrameworkCore.Analyzers                         10.0.14     
   > Microsoft.EntityFrameworkCore.Relational                        10.0.14     
   > Microsoft.Extensions.AI                                         10.11.0     
   > Microsoft.Extensions.AI.Abstractions                            10.11.0     
   > Microsoft.Extensions.AI.Evaluation                              10.11.0     
   > Microsoft.Extensions.AmbientMetadata.Application                10.11.0     
   > Microsoft.Extensions.Caching.Abstractions                       10.0.14     
   > Microsoft.Extensions.Caching.Memory                             10.0.14     
   > Microsoft.Extensions.Compliance.Abstractions                    10.11.0     
   > Microsoft.Extensions.Configuration                              10.0.14     
   > Microsoft.Extensions.Configuration.Abstractions                 10.0.14     
   > Microsoft.Extensions.Configuration.Binder                       10.0.14     
   > Microsoft.Extensions.Configuration.CommandLine                  10.0.14     
   > Microsoft.Extensions.Configuration.EnvironmentVariables         10.0.14     
   > Microsoft.Extensions.Configuration.FileExtensions               10.0.14     
   > Microsoft.Extensions.Configuration.Json                         10.0.14     
   > Microsoft.Extensions.Configuration.UserSecrets                  10.0.14     
   > Microsoft.Extensions.DependencyInjection                        10.0.14     
   > Microsoft.Extensions.DependencyInjection.Abstractions           10.0.14     
   > Microsoft.Extensions.DependencyInjection.AutoActivation         10.11.0     
   > Microsoft.Extensions.DependencyModel                            10.0.14     
   > Microsoft.Extensions.Diagnostics                                10.0.14     
   > Microsoft.Extensions.Diagnostics.Abstractions                   10.0.14     
   > Microsoft.Extensions.Diagnostics.ExceptionSummarization         10.11.0     
   > Microsoft.Extensions.Diagnostics.HealthChecks                   8.0.11      
   > Microsoft.Extensions.Diagnostics.HealthChecks.Abstractions      8.0.11      
   > Microsoft.Extensions.FileProviders.Abstractions                 10.0.14     
   > Microsoft.Extensions.FileProviders.Physical                     10.0.14     
   > Microsoft.Extensions.FileSystemGlobbing                         10.0.14     
   > Microsoft.Extensions.Hosting.Abstractions                       10.0.14     
   > Microsoft.Extensions.Http.Diagnostics                           10.11.0     
   > Microsoft.Extensions.Logging                                    10.0.14     
   > Microsoft.Extensions.Logging.Abstractions                       10.0.14     
   > Microsoft.Extensions.Logging.Configuration                      10.0.14     
   > Microsoft.Extensions.Logging.Console                            10.0.14     
   > Microsoft.Extensions.Logging.Debug                              10.0.14     
   > Microsoft.Extensions.Logging.EventLog                           10.0.14     
   > Microsoft.Extensions.Logging.EventSource                        10.0.14     
   > Microsoft.Extensions.ObjectPool                                 10.0.13     
   > Microsoft.Extensions.Options                                    10.0.14     
   > Microsoft.Extensions.Options.ConfigurationExtensions            10.0.14     
   > Microsoft.Extensions.Primitives                                 10.0.14     
   > Microsoft.Extensions.Resilience                                 10.11.0     
   > Microsoft.Extensions.Telemetry                                  10.11.0     
   > Microsoft.Extensions.Telemetry.Abstractions                     10.11.0     
   > Microsoft.Extensions.VectorData.Abstractions                    10.10.0     
   > Microsoft.IdentityModel.Abstractions                            8.23.0      
   > Microsoft.IdentityModel.JsonWebTokens                           8.23.0      
   > Microsoft.IdentityModel.Logging                                 8.23.0      
   > Microsoft.IdentityModel.Tokens                                  8.23.0      
   > Microsoft.ML.Tokenizers                                         2.0.0       
   > Microsoft.SemanticKernel.Abstractions                           1.81.0      
   > Microsoft.SemanticKernel.Connectors.AzureOpenAI                 1.81.0      
   > Microsoft.SemanticKernel.Connectors.OpenAI                      1.81.0      
   > Microsoft.SemanticKernel.Core                                   1.81.0      
   > Microsoft.VisualStudio.SolutionPersistence                      1.0.52      
   > MimeKit                                                         4.18.2      
   > Mono.TextTemplating                                             3.0.0       
   > Npgsql                                                          10.0.3      
   > OpenAI                                                          2.14.0      
   > Polly.Core                                                      8.4.2       
   > Polly.Extensions                                                8.4.2       
   > Polly.RateLimiting                                              8.4.2       
   > Serilog.Extensions.Hosting                                      10.0.0      
   > Serilog.Extensions.Logging                                      10.0.0      
   > Serilog.Settings.Configuration                                  10.0.0      
   > Serilog.Sinks.Console                                           6.1.1       
   > Serilog.Sinks.Debug                                             3.0.0       
   > Serilog.Sinks.File                                              7.0.0       
   > System.ClientModel                                              1.15.0      
   > System.CodeDom                                                  6.0.0       
   > System.Composition                                              9.0.0       
   > System.Composition.AttributedModel                              9.0.0       
   > System.Composition.Convention                                   9.0.0       
   > System.Composition.Hosting                                      9.0.0       
   > System.Composition.Runtime                                      9.0.0       
   > System.Composition.TypedParts                                   9.0.0       
   > System.Configuration.ConfigurationManager                       10.0.14     
   > System.Diagnostics.EventLog                                     10.0.14     
   > System.Memory.Data                                              10.0.9      
   > System.Numerics.Tensors                                         10.0.13     
   > System.Security.Cryptography.ProtectedData                      10.0.14     
   > System.Threading.RateLimiting                                   8.0.0       
   > YamlDotNet                                                      18.0.0      

```
