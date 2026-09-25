using System;
using xFunc.Maths;
using xFunc.Maths.Results;

namespace Batchbuild
{
    // The xFunc processor is deliberately kept in an object field instead of a Processor field.
    // A field typed with an xFunc.Maths type forces the runtime to resolve the xFunc.Maths
    // assembly as soon as this type is loaded. Script Engine reflects over every type right
    // after loading the DLL, before Costura (which embeds xFunc.Maths) has had a chance to
    // register its assembly resolver in the module constructor, which threw a TypeLoadException.
    // BepInEx only loads the Plugin type, so it never noticed.
    // xFunc types are now only referenced inside method bodies: those are resolved when the
    // method is JITted (first command entered), by which time the resolver is in place.
    static class Lib
    {
        private static object processor;

        public static float Evaluate(string expression)
        {
            if (processor == null)
            {
                processor = new Processor();
            }

            return Convert.ToSingle(((Processor)processor).Solve<NumberResult>(expression).Result);
        }
    }
}
