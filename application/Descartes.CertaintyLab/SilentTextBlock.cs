using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Descartes.CertaintyLab;

public sealed class SilentTextBlock : TextBlock
{
    protected override AutomationPeer? OnCreateAutomationPeer() => null;
}

public sealed class SilentItemsControl : ItemsControl
{
    protected override AutomationPeer? OnCreateAutomationPeer() => null;
}

public sealed class ActionItemsControl : ItemsControl
{
    protected override AutomationPeer OnCreateAutomationPeer() =>
        new ActionItemsControlAutomationPeer(this);
}

internal sealed class ActionItemsControlAutomationPeer(
    ActionItemsControl owner) : ItemsControlAutomationPeer(owner)
{
    protected override ItemAutomationPeer CreateItemAutomationPeer(object item) =>
        new ActionItemAutomationPeer(item, this);
}

internal sealed class ActionItemAutomationPeer(
    object item,
    ItemsControlAutomationPeer parent) : ItemAutomationPeer(item, parent)
{
    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Custom;

    protected override string GetClassNameCore() =>
        nameof(ActionItemAutomationPeer);

    protected override bool IsControlElementCore() => false;

    protected override bool IsContentElementCore() => false;
}

public sealed class AtomicSummaryBorder : Border
{
    public void RaiseLiveRegionChanged()
    {
        AutomationPeer? peer = UIElementAutomationPeer.FromElement(this) ??
            UIElementAutomationPeer.CreatePeerForElement(this);
        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    protected override AutomationPeer OnCreateAutomationPeer() =>
        new AtomicSummaryBorderAutomationPeer(this);
}

internal sealed class AtomicSummaryBorderAutomationPeer(
    AtomicSummaryBorder owner) : FrameworkElementAutomationPeer(owner)
{
    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Group;

    protected override string GetClassNameCore() =>
        nameof(AtomicSummaryBorder);
}
