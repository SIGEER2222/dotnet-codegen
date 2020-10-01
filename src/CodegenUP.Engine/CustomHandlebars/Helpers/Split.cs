﻿using HandlebarsDotNet;
using System;
using System.IO;
using System.Linq;

namespace CodegenUP.CustomHandlebars.Helpers
{
    /// <summary>
    /// Split the string & return the last chunk
    /// </summary>
#if DEBUG
    [HandlebarsHelperSpecification("{ '$ref' : '#/parameters/myDataType'}", "{{split_get_last ./$ref '/' }}", "myDataType")]
#endif
    public class SplitGetLast : SimpleStandardHelperBase
    {
        public SplitGetLast() : base("split_get_last") { }

        public override void Helper(TextWriter output, object context, object[] arguments)
        {
            EnsureArgumentsCount(arguments, 2);

            var argument = arguments[0].ToString();
            var splitter = arguments[1].ToString();

            output.Write(argument?.Split(new[] { splitter }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "");
        }
    }

    /// <summary>
    /// Split the string & return the first chunk
    /// </summary>
#if DEBUG
    [HandlebarsHelperSpecification("{ '$ref' : '/myDataType/parameters/'}", "{{split_get_first ./$ref '/' }}", "myDataType")]
#endif
    public class SplitGetFirst : SimpleStandardHelperBase
    {
        public SplitGetFirst() : base("split_get_first") { }

        public override void Helper(TextWriter output, object context, object[] arguments)
        {
            EnsureArgumentsCount(arguments, 2);

            var argument = arguments[0].ToString();
            var splitter = arguments[1].ToString();

            output.Write(argument?.Split(new[] { splitter }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "");
        }
    }
}
