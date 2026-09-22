---
name: write-xunit-tests
description: Write xUnit tests for a .NET class or method. Use when the user asks to write tests, add test coverage, or generate tests for a specific file or method.
---

## Instructions

1. Read the target file to understand the class and its dependencies.
2. Read AGENTS.md for the project's test naming conventions.
3. Write tests using xUnit, NSubstitute for mocks, and FluentAssertions for assertions.
4. Follow the naming convention: {ClassName}Tests and {MethodName}_{Scenario}_{ExpectedResult}.
5. Cover: happy path, validation failures, edge cases, and any async cancellation paths.
6. Use constructor injection for the class under test. Do not use static factories or service locators in tests.
7. Run `dotnet test` on the test project and fix any failures before presenting the output.