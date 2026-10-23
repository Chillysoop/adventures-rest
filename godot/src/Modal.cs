using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// Cards that interrupt play to be read (a first meeting, the season's recap) sit on their own layer above every
/// window, tray and notice, over a dimmed screen that takes the clicks meant for what lies under it. A click on the
/// dim closes the card the way Esc does, so nothing can hide one or trap the player behind it. Each frame the shown
/// card is held at its own size in the middle of the screen, whatever the layout did to it on the way in.
/// </summary>
public partial class Main
{
    private Control _modalRoot = null!;
    private ColorRect _scrim = null!;

    private void BuildModal()
    {
        var layer = new CanvasLayer { Layer = 10 };
        AddChild(layer);
        _modalRoot = new Control { MouseFilter = MouseFilterEnum.Ignore, Theme = Theme };
        layer.AddChild(_modalRoot);
        _modalRoot.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _scrim = new ColorRect { Color = new Color(0.04f, 0.03f, 0.02f, 0.45f), Visible = false, MouseFilter = MouseFilterEnum.Stop };
        _modalRoot.AddChild(_scrim);
        _scrim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _scrim.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left or MouseButton.Right }) DismissModal();
        };
    }

    /// <summary>A card for the modal layer, centred and grown both ways from the middle.</summary>
    private void AddModal(PanelContainer card)
    {
        _modalRoot.AddChild(card);
        card.SetAnchorsAndOffsetsPreset(LayoutPreset.Center, LayoutPresetMode.Minsize);
        card.GrowHorizontal = Control.GrowDirection.Both;
        card.GrowVertical = Control.GrowDirection.Both;
        card.MinimumSizeChanged += () => Ui.Refit(card);
    }

    private Control? TopModal() => _intro.Visible ? _intro : _recap.Visible ? _recap : null;

    private void DismissModal()
    {
        if (_intro.Visible) CloseIntro();
        else if (_recap.Visible) _recap.Visible = false;
    }

    private void SyncModal()
    {
        var top = TopModal();
        _scrim.Visible = top != null;
        if (top == null) return;
        if (top.GetIndex() != _modalRoot.GetChildCount() - 1) top.MoveToFront();
        Fit(top);
        // Text measured before it had a width can leave a card taller than the screen; lay its text out afresh.
        if (top == _intro && _intro.Size.Y > _modalRoot.Size.Y && _introRedo-- > 0) RenderIntro();
    }

    /// <summary>Holds a card at its own size, centred: never bigger than its content, never pushed off the screen.</summary>
    internal static void Fit(Control card)
    {
        var min = card.GetCombinedMinimumSize();
        var view = card.GetParentAreaSize();
        var at = ((view - card.Size) / 2).Floor();
        if (Mathf.Abs(card.Size.X - min.X) < 1 && Mathf.Abs(card.Size.Y - min.Y) < 1 && card.Position.DistanceTo(at) < 2) return;
        card.ResetSize();
        card.SetAnchorsAndOffsetsPreset(LayoutPreset.Center, LayoutPresetMode.Minsize);
    }
}
