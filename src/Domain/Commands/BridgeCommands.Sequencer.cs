namespace Snail.MCP.Blender.Domain.Commands;

/// <summary>Commands of <c>addon/sequencer.py</c>: the Video Sequence Editor: strips, cuts, grading, encoding and mixdown.</summary>
public static partial class BridgeCommands
{
    public static readonly BridgeCommand SequencerInfo = new("sequencer_info", AddOnModules.Sequencer);

    public static readonly BridgeCommand SequencerAddStrip = new("sequencer_add_strip", AddOnModules.Sequencer);

    public static readonly BridgeCommand SequencerAddEffect = new("sequencer_add_effect", AddOnModules.Sequencer);

    public static readonly BridgeCommand SequencerUpdateStrip = new("sequencer_update_strip", AddOnModules.Sequencer);

    public static readonly BridgeCommand SequencerRemoveStrip = new("sequencer_remove_strip", AddOnModules.Sequencer);

    public static readonly BridgeCommand SequencerRender = new("sequencer_render", AddOnModules.Sequencer);

    public static readonly BridgeCommand SequencerSplit = new("sequencer_split", AddOnModules.Sequencer);

    public static readonly BridgeCommand SequencerProxy = new("sequencer_proxy", AddOnModules.Sequencer);

    public static readonly BridgeCommand SequencerMeta = new("sequencer_meta", AddOnModules.Sequencer);

    public static readonly BridgeCommand SequencerTiming = new("sequencer_timing", AddOnModules.Sequencer);

    public static readonly BridgeCommand SequencerGrade = new("sequencer_grade", AddOnModules.Sequencer);

    public static readonly BridgeCommand SequencerEncode = new("sequencer_encode", AddOnModules.Sequencer);

    public static readonly BridgeCommand SequencerMixdown = new("sequencer_mixdown", AddOnModules.Sequencer);
}
