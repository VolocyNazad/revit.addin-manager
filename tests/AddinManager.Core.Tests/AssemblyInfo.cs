using Xunit.Sdk;
using Xunit.v3;

// Тесты, которые временно меняют культуру (TestCulture), не должны пересекаться с параллельными:
// DefaultThreadCurrentUICulture — глобальное состояние, и соседний тест может увидеть чужую культуру.
[assembly: Parallelization(Mode = ParallelMode.None)]
