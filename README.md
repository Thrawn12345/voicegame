# Modern C# Console Application

This project demonstrates a simple console application built with modern .NET and C# features.

## Features

- **.NET 8**: The latest long-term support (LTS) version of .NET.
- **Top-Level Statements**: The `Program.cs` file uses top-level statements, which simplifies the code by removing the need for an explicit `Main` method and `Program` class.
- **Implicit Usings**: The project file has `<ImplicitUsings>enable</ImplicitUsings>`, which allows the C# compiler to automatically add common `global using` directives.
- **File-Scoped Namespaces**: (If you were to add more classes in separate files, you could use this feature).
- **Records**: A concise syntax for creating immutable reference types.

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## How to Run

1.  **Clone the repository** (or have the files on your local machine).
2.  **Open a terminal** in the project's root directory.
3.  **Run the application** using the following command:

    ```bash
    dotnet run
    ```

## Project Structure

-   `ModernCSharp.csproj`: The C# project file, configured for .NET 8.
-   `src/Program.cs`: The main application entry point, written using top-level statements.
-   `README.md`: This file.

This setup provides a clean and minimal starting point for a modern C# console application.
