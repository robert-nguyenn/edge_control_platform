# Edge Control Platform Contributions

We welcome contributions from the community! This document outlines the process for contributing to the Edge Control Platform.

## Getting Started

1. **Fork the repository** on GitHub
2. **Clone your fork** to your local machine
3. **Create a branch** for your feature or bugfix
4. **Make your changes**
5. **Write or update tests** for your changes
6. **Submit a pull request**

## Development Environment Setup

1. Install required dependencies:
   - .NET 8 SDK
   - Node.js 18+
   - Java 11+
   - Go 1.21+
   - C++ compiler with C++20 support
   - Docker and Docker Compose

2. Set up the development environment:

```bash
# Clone the repository
git clone https://github.com/robert-nguyenn/edge_control_platform.git
cd edge_control_platform

# Start the development environment
cd ops
make dev
```

## Code Style and Standards

### .NET
- Follow Microsoft's [C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use asynchronous programming patterns with `async`/`await`
- Include XML documentation for public APIs

### Go
- Follow the [Go Code Review Comments](https://github.com/golang/go/wiki/CodeReviewComments)
- Use `gofmt` for formatting
- Write idiomatic Go code with proper error handling

### C++
- Follow the [Google C++ Style Guide](https://google.github.io/styleguide/cppguide.html)
- Use modern C++20 features appropriately
- Ensure memory safety and proper resource management

### TypeScript/JavaScript
- Follow the [Airbnb JavaScript Style Guide](https://github.com/airbnb/javascript)
- Use TypeScript interfaces for type safety
- Use ESLint for code quality

## Testing

Each component should include appropriate tests:

- **Unit tests** for individual functions and classes
- **Integration tests** for component interactions
- **End-to-end tests** for complete workflows

Run tests before submitting a pull request:

```bash
# Run .NET tests
cd api-dotnet
dotnet test

# Run Node.js SDK tests
cd sdks/node
npm test

# Run Go sidecar tests
cd go-sidecar
go test ./...

# Run C++ rate limiter tests
cd rate-limiter-cpp
./build_and_test.sh

# Run smoke tests
cd tests
./smoke-test.sh
```

## Pull Request Process

1. Update the README.md and documentation with details of changes
2. Update the version numbers in relevant files following [Semantic Versioning](https://semver.org/)
3. Ensure all tests pass
4. The PR will be merged once you have the sign-off of at least one maintainer

## Code of Conduct

Please follow our [Code of Conduct](CODE_OF_CONDUCT.md) in all your interactions with the project.

## License

By contributing, you agree that your contributions will be licensed under the project's [MIT License](LICENSE).
