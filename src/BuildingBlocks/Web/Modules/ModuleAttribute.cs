namespace EDV.Framework.Web.Modules;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ModuleAttribute : Attribute
{
    public Type ModuleType { get; }

    /// <summary>
    /// Необязательная подсказка для порядка, позволяющая хостам управлять последовательностью запуска модулей.
    /// Меньшие значения выполняются первыми.
    /// </summary>
    public int Order { get; }

    public ModuleAttribute(Type moduleType, int order = 0)
    {
        ModuleType = moduleType ?? throw new ArgumentNullException(nameof(moduleType));
        Order = order;
    }
}