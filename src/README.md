# Clean Architecture Solution Template Using Domain Driven Design

This template is a starting point for a clean architecture project using Domain Driven Design. It is a simple project
that contains the basic structure and configuration to start a new project.

### CQRS vs Service classes
#### For medium to large projects:
The solution uses the CQRS pattern to separate the read and write operations. The read operations are handled by queries and the write operations are handled by commands.
Single responsibility commands/queries are preferred over large service classes to handle the operations.
#### For small projects:
Service classes are feasible, as the number of commands/queries is small and the operations are simple, so it is easier to manage the operations.

### Splitting by feature vs splitting by type
#### For medium to large projects:
Following the Domain Driven Design principles, the different projects have a folder structure separated by features rather than component type,
in other words, instead of having a top level folder with all the controllers, another with all the services, etc.,
the projects are structured by feature, so all the components related to a feature are in the same folder in the corresponding project; this is done so
that the components that are related to a feature are close to each other and easier to find, especially in this case where
the implementation of a particular feature is spread across different projects.
#### For small projects:
Splitting by type is feasible, as the number of features is small and the number of components is also small, so it is easier to find the components by type.

### Domain events vs orchestration
#### For medium to large projects:
The solution uses domain events to coordinate the interaction between different domain objects.
The domain events are used to notify the interested parties of the changes in the domain objects.
When a domain object changes its state, it raises a domain event that is handled by the interested parties;
this allows the domain objects to be decoupled from each other.
#### For small projects:
Orchestration is feasible, as the number of domain objects is small and the interactions between them are simple, so it is easier to manage the interactions between them.

## Solution overview
Below is a brief description of the structure of the projects.

### WebApi
- Presentation layer of the project
- Handles the HTTP requests from the client
- Manipulates the requests and sends them to the application layer using the MediatR library
- **Contains the following components**
    - Controllers: Classes that receive the requests from the client
    - DependencyInjection: Contains the configuration for the dependency injection of the presentation layer
    - Program file: Contains the configuration for the web host

### Contracts
- Defines contracts for the presentation layer
- **Contains the following components**
    - Requests: Classes that represent the requests from the presentation layer
    - Responses: Classes that represent the responses to the presentation layer

### Application
- Application layer of the project
- Uses the MediatR library to receive requests from the presentation layer
- Orchestrates the interaction of different domain objects to fulfill the requests
- **Contains the following components**
    - Interfaces to be implemented by the infrastructure layer (Repository Interfaces, Adapter Interfaces, etc.)
    - Commands: Classes that represent the requests from the presentation layer
    - Queries: Classes that represent the requests from the presentation layer
    - Validators: Classes that validate the commands and queries
    - Command/query handlers: Classes that handle the requests from the presentation layer
    - Event handlers: Classes that handle the domain events
    - DependencyInjection: Contains the configuration for the dependency injection of the application layer

### Domain
- Domain layer of the project
- Defines the domain models
- Defines the domain errors
- Executes business logic and rules
- **Contains the following components**
    - Entities
    - Value objects
    - Domain events

### Infrastructure
- Infrastructure layer of the project
- Interacts with external systems (Database, Message brokers, File System, etc.)
- Contains the implementation of the interfaces defined in the application layer (Repositories, Adapters, etc.)
- **Contains the following components**
    - Persistence: Contains the configuration for the database context and the migrations
    - Repositories: Classes that implement the repository interfaces defined in the application layer
    - Adapters: Classes that implement the adapter interfaces defined in the application layer
    - DependencyInjection: Contains the configuration for the dependency injection of the infrastructure layer

### Worker
- Entry point for execution of background tasks
- Design to be executed separately from the API and to be used for long running tasks
- **Contains the following components**
    - Scheduled task management: Contains the configuration for the scheduled tasks
    - Message queue consumers: Contains the configuration for the message queue consumers
    - Background tasks: Contains the configuration for the background tasks
    - DependencyInjection: Contains the configuration for the dependency injection of the worker

## EF Core Migrations
The project is intended to use EF Core for data access. The project is configured to use migrations to manage the database schema using the code-first approach.

The commands below can be used for the commonly used EF Core migration commands.

#### Create a migration from the current models
The commands below must be executed from the src folder.
Create a migration by analyzing the current models and generating the necessary code to update the database schema.
```bash
dotnet ef migrations add <MigrationName> --context AppDbContext --project Infrastructure --startup-project Api -- --environment development
```
#### Remove the last migration
Remove the last migration that was added to the project
```bash
dotnet ef migrations remove --project Infrastructure --startup-project Api -- --environment development
```
#### Update the database with the latest migration
Update the database with the latest migration that was added to the project
```bash
dotnet ef database update --project Infrastructure --startup-project Api -- --environment development
```
#### List the migrations
List the migrations that have been applied to the database
```bash
dotnet ef migrations list --project Infrastructure --startup-project Api -- --environment development
```
#### Script the migration
This is useful to hand-off sql scripts to the DBA for deployment
```bash
dotnet ef migrations script --project Infrastructure --startup-project Api -- --environment development
```

## Non-framework packages
The template uses the following packages that are not part of the .NET framework:
- MediatR: Library that implements the mediator pattern to handle requests and responses
- FluentValidation: Library that implements the validation pattern to validate input data
- AutoMapper: Library that implements the mapping pattern to map objects from one type to another
- NLog.Extensions.Logging: Library that implements logging
- NLog.Targets.KafkaAppender: Library that implements the NLog target for Kafka
- ErrorOr: Library that implements the result pattern to handle errors and results
- Throw: Library for fluent exception handling
- Swashbuckle.AspNetCore: Library that implements the swagger documentation for the API
- Ardalis.SmartEnum: Library that implements the smart enums
- CDev.Utility: Library that implements the utility classes

### Closing remarks
This template follows closely the definition by Uncle Bob in his book Clean Architecture, as well as the repository by Amichai Mantinband at https://github.com/amantinband/clean-architecture.

When creating a new solution from this template replace this readme.md file with documentation for the solution being implemented or delete it as confirmation of reading it.
