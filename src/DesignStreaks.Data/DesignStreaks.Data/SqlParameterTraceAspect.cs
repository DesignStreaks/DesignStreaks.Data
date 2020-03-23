// *
// * DESIGNSTREAKS CONFIDENTIAL
// * __________________
// *
// *  Copyright © Design Streaks - 2010 - 2018
// *  All Rights Reserved.
// *
// * NOTICE:  All information contained herein is, and remains
// * the property of DesignStreaks and its suppliers, if any.
// * The intellectual and technical concepts contained
// * herein are proprietary to DesignStreaks and its suppliers and may
// * be covered by Australian, U.S. and Foreign Patents,
// * patents in process, and are protected by trade secret or copyright law.
// * Dissemination of this information or reproduction of this material
// * is strictly forbidden unless prior written permission is obtained
// * from DesignStreaks.

namespace DesignStreaks.Data
{
    using System;
    using System.Data.Common;
    using System.Diagnostics;
    using PostSharp.Aspects;

    /// <summary>Logging Aspect used to log Sql stored procedure details to the debug console.</summary>
    [Serializable]
    public class SqlParameterTraceAspect : OnMethodBoundaryAspect
    {
        private long startTick = 0;

        /// <summary>Method executed <b>before</b> the body of methods to which this aspect is applied.</summary>
        /// <param name="args">
        ///   Event arguments specifying which method is being executed, which are its arguments, and how should the execution continue after
        ///   the execution of <see cref="M:PostSharp.Aspects.IOnMethodBoundaryAspect.OnEntry(PostSharp.Aspects.MethodExecutionArgs)" />.
        /// </param>
        [DebuggerHidden]
        public sealed override void OnEntry(MethodExecutionArgs args)
        {
            startTick = DateTime.Now.Ticks;

            string[] parameters = ExtractParameters(args);

            Trace.TraceInformation(
                        "{0:HH:mm:ss.fff}:\t--> [{1,5}]\t\t{2} {3}",
                        DateTime.Now,
                        System.Threading.Thread.CurrentThread.ManagedThreadId,
                        args.Arguments[0],
                        string.Join(", ", parameters));
            base.OnEntry(args);
        }

        /// <summary>
        ///   Method executed <b>after</b> the body of methods to which this aspect is applied, even when the method exists with an exception
        ///   (this method is invoked from the <c>finally</c> block).
        /// </summary>
        /// <param name="args">Event arguments specifying which method is being executed and which are its arguments.</param>
        [DebuggerHidden]
        public sealed override void OnExit(MethodExecutionArgs args)
        {
            long endTick = DateTime.Now.Ticks;

            string[] parameters = ExtractParameters(args);

            Trace.TraceInformation(
                        "{0:HH:mm:ss.fff}:\t<-- [{1,5}]\t\t{2}\t:\t{3}ms",
                        DateTime.Now,
                        System.Threading.Thread.CurrentThread.ManagedThreadId,
                        args.Arguments[0],
                        new TimeSpan(endTick - this.startTick).TotalMilliseconds);

            base.OnExit(args);
        }

        [DebuggerHidden]
        private static string[] ExtractParameters(MethodExecutionArgs args)
        {
            var numArgs = (args.Arguments[1] as DbParameter[]).Length;

            string[] parameters = new string[numArgs];

            for (int index = 0; index < numArgs; index++)
            {
                parameters[index] = (args.Arguments[1] as DbParameter[])[index].ToString(true);
            }
            return parameters;
        }
    }
}