using JROptimizerPro.Models;

namespace JROptimizerPro.Services;

internal static class PerformanceProfileCatalog
{
    public static IReadOnlyList<PerformanceProfileDefinition> Create(HardwareProfile hardware) =>
        new[]
        {
            new PerformanceProfileDefinition(
                PerformanceProfileType.DayToDay,
                "Dia a dia",
                hardware.IsLowMemory
                    ? "Recomendado para este PC: reduz efeitos e conteúdo secundário sem limitar pesquisa ou serviços."
                    : "Equilíbrio entre resposta, recursos visuais e consumo de energia.",
                new OptimizationOptions
                {
                    DisableTransparency = hardware.IsLowMemory,
                    ReduceAnimations = hardware.IsLowMemory,
                    DisableGameDvr = true,
                    DisableWebSearch = true,
                    DisableSuggestions = true,
                    DisableWidgets = hardware.IsLowMemory,
                    PowerPlan = PowerPlanMode.Balanced
                }),
            new PerformanceProfileDefinition(
                PerformanceProfileType.Gamer,
                "Modo Gamer",
                "Prioriza resposta e estabilidade durante jogos; desliga capturas do Windows e efeitos dispensáveis.",
                new OptimizationOptions
                {
                    DisableTransparency = true,
                    ReduceAnimations = true,
                    DisableGameDvr = true,
                    DisableWebSearch = true,
                    DisableSuggestions = true,
                    DisableWidgets = true,
                    DisableBackgroundApps = hardware.IsLowMemory,
                    PowerPlan = PowerPlanMode.HighPerformance
                }),
            new PerformanceProfileDefinition(
                PerformanceProfileType.Multitasking,
                "Multitarefa",
                "Preserva aplicativos em segundo plano e indexação, reduzindo somente efeitos e conteúdo promocional.",
                new OptimizationOptions
                {
                    DisableTransparency = hardware.IsLowMemory,
                    ReduceAnimations = hardware.IsLowMemory,
                    DisableGameDvr = true,
                    DisableWebSearch = false,
                    DisableSuggestions = true,
                    DisableWidgets = hardware.IsLowMemory,
                    DisableBackgroundApps = false,
                    PowerPlan = PowerPlanMode.Balanced
                }),
            new PerformanceProfileDefinition(
                PerformanceProfileType.Streaming,
                "Modo Stream",
                "Prioriza energia e recursos para OBS, câmera e áudio, sem bloquear aplicativos em segundo plano.",
                new OptimizationOptions
                {
                    DisableTransparency = true,
                    ReduceAnimations = true,
                    DisableGameDvr = true,
                    DisableWebSearch = true,
                    DisableSuggestions = true,
                    DisableWidgets = true,
                    DisableBackgroundApps = false,
                    PowerPlan = PowerPlanMode.HighPerformance
                }),
            new PerformanceProfileDefinition(
                PerformanceProfileType.Economy,
                "Economia de bateria",
                "Reduz efeitos, capturas e atividade secundária e ativa o plano de economia de energia.",
                new OptimizationOptions
                {
                    DisableTransparency = true,
                    ReduceAnimations = true,
                    DisableGameDvr = true,
                    DisableWebSearch = true,
                    DisableSuggestions = true,
                    DisableWidgets = true,
                    DisableBackgroundApps = true,
                    PowerPlan = PowerPlanMode.PowerSaver
                })
        };
}
