namespace SampleApp.Resources
{
    /// <summary>
    /// Stands in for the resource classes a real DotVVM project generates from .resx files.
    /// The master pages import this namespace and the views bind to it, so without it every
    /// `{resource: …}` in the fixture fails to compile.
    /// </summary>
    public static class Labels
    {
        public static string Save => "Save";
    }
}
