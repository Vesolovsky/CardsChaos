namespace Vesolovsky.Core.Utils
{
    /// <summary>
    /// Marks a component as a development tool - a cheat, a capture rig, a test harness - that has
    /// no business in a build players will see.
    ///
    /// Two things act on it, and both are needed because they cover different halves of the same
    /// problem. The tools themselves are compiled behind
    /// <c>#if UNITY_EDITOR || CARDSCHAOS_DEBUG_TOOLS</c>, so their code is simply not in an ordinary
    /// build; and the build strips every object carrying one of them out of the scene, so the scene
    /// is not left pointing at a script that is no longer there.
    ///
    /// Adding the <c>CARDSCHAOS_DEBUG_TOOLS</c> define (Vesolovsky > Debug Tools > Include In
    /// Builds) puts both halves back, for a build made to record or test with.
    ///
    /// The interface is deliberately empty and deliberately not compiled out with the tools: it
    /// costs nothing, and it is what the build has to look for.
    /// </summary>
    public interface IDebugTool
    {
    }
}
