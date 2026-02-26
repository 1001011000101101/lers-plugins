using System.Reflection;
using Lers.Plugins;

// Plugin attributes required for LERS registration
[assembly: AssemblyPlugin("F7A8B9C0-D1E2-4F5A-6B7C-8D9E0F1A2B3C")]
[assembly: AssemblyPluginEmail("0406590@gmail.com")]
[assembly: AssemblyPluginWebsite("https://lersassistant.ru")]
// InfoUrl removed - may cause update check issues
// [assembly: AssemblyPluginInfoUrl("https://lersassistant.ru/plugins/report-generator")]

// Standard assembly attributes
[assembly: AssemblyTitle("Генератор отчётов ЛЭРС")]
[assembly: AssemblyDescription("Плагин массовой генерации отчётов для ЛЭРС УЧЁТ")]
[assembly: AssemblyCompany("Матюшкин Роман")]
[assembly: AssemblyProduct("LersReportGeneratorPlugin")]
[assembly: AssemblyCopyright("Copyright 2026 Матюшкин Роман")]
[assembly: AssemblyVersion("1.0.41.0")]
[assembly: AssemblyFileVersion("1.0.41.0")]
