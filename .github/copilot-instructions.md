- [x] Verify that the copilot-instructions.md file in the .github directory is created.

- [x] Clarify Project Requirements
	**Completed**: Azure AI Agent with C#/.NET 8, Semantic Kernel, Web API + Console App

- [x] Scaffold the Project
	**Completed**: Created .NET 8 solution with Console, API, Core, Plugins, and Tests projects. All packages restored successfully.

- [x] Customize the Project
	**Completed**: Implemented full Azure AI Agent architecture with:
	- Core logic with AI agent, session management, and Azure operations
	- Semantic Kernel plugins for Azure resource management
	- Hybrid approach: Template-based, Command-based, and SDK-based operations
	- Console application with interactive natural language interface
	- Comprehensive service layer with proper dependency injection
	- Enhanced auto-resolution capabilities for deployment issues
	- Automatic resource import to prevent recreation during partial failures
	- README with detailed setup and usage instructions

- [x] Install Required Extensions
	**Skipped**: No specific extensions required for this project type.

- [x] Compile the Project
	**Completed**: All projects compile successfully. Solution build passes without errors.
	- Verified all package references and dependencies are correctly installed
	- All interface implementations are complete and functional
	- No compilation errors or warnings that block functionality
	- Enhanced auto-resolution features verified and working

- [x] Create and Run Task
	**Skipped**: This is a console application. Tasks are not required for this project type. Users can run with `dotnet run`.

- [x] Launch the Project
	**Ready for Launch**: Project is ready to be launched. User needs to:
	1. Set OpenAI API key: `export OPENAI_API_KEY="your-key"` OR configure Azure OpenAI in appsettings.json
	2. Login to Azure CLI: `az login`
	3. Run the console app: `cd AzureAIAgent.Console && dotnet run`

- [x] Ensure Documentation is Complete
	**Completed**: All previous steps have been completed successfully.
	- README.md contains comprehensive project information and setup instructions
	- copilot-instructions.md exists in .github directory with current project status
	- All HTML comments have been cleaned up
	- Enhanced auto-resolution and partial deployment failure handling is documented

## Project-Specific Instructions for Azure AI Agent (.NET/C#)

This is an Azure AI Agent solution using **C# and .NET** with Semantic Kernel to create and manage Azure resources through natural language commands.

### Architecture:
- **Backend**: .NET 8 with C# 
- **AI Framework**: Semantic Kernel (native C# implementation)
- **Azure Integration**: Azure SDK for .NET, Bicep templates, Azure CLI
- **API**: ASP.NET Core Web API
- **Console**: .NET Console Application
- **Authentication**: Azure.Identity with DefaultAzureCredential

### Key Components:
1. **AI Agent Core**: Semantic Kernel-based orchestration in C#
2. **Azure Plugins**: Custom skills for Azure resource management
3. **Planning Engine**: Automatic task breakdown and execution
4. **Memory Management**: Context and conversation history
5. **Bicep Generator**: Infrastructure as Code generation
6. **Web API**: RESTful API for integration
7. **Console App**: Interactive command-line interface
8. **Enhanced Auto-Resolution**: Intelligent error handling and resource import

### Enhanced Features:
- **Automatic Error Resolution**: Detects and fixes common deployment issues
- **Resource Import**: Automatically imports existing Azure resources to prevent recreation
- **Version Validation**: Real-time validation of AKS versions and region compatibility
- **Region Auto-Selection**: Intelligent region switching based on resource availability
- **Partial Failure Recovery**: Handles partial deployment failures gracefully

### Development Guidelines:
- Use .NET 8 with C# 12 features
- Follow Azure SDK for .NET best practices
- Implement proper dependency injection
- Use structured logging with Serilog
- Generate secure Bicep templates
- Implement proper Azure authentication
- Use async/await patterns throughout
- Leverage enhanced auto-resolution for seamless deployments
