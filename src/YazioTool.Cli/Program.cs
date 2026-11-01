using Microsoft.Extensions.DependencyInjection;
using YazioTool.Cli;

using var services = CliServices.Build();
var commands = ActivatorUtilities.CreateInstance<DiaryCommands>(services);

return await commands.CreateRootCommand().Parse(args).InvokeAsync();
