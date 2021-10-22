using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace WebApplication1
{
    public class JaegerPropagator : TextMapPropagator
    {
        public override ISet<string> Fields => new HashSet<string> { JaegerHeader };

        private const string JaegerHeader = "uber-trace-id";
        private const string Separator = ":";

        private const int TraceId64bitsWidth = 64 / 4;
        private const int TraceId128bitsWidth = 128 / 4;
        private const int SpanIdWidth = 64 / 4;

        private const int FlagsDebug = 0x02;
        private const int FlagsSampled = 0x01;
        private const int FlagsNotSampled = 0x00;

        private const string DeprecatedParentSpanId = "0";


        public override void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter)
        {
            if (context.ActivityContext.TraceId == default
                || context.ActivityContext.SpanId == default
                || carrier == null || setter == null)
            {
                return;
            }

            var headers = new List<string>
            {
                context.ActivityContext.TraceId.ToHexString(),
                context.ActivityContext.SpanId.ToHexString(),
                DeprecatedParentSpanId,
            };

            if ((context.ActivityContext.TraceFlags & ActivityTraceFlags.Recorded) != 0)
                headers.Add(FlagsSampled.ToString());
            else
                headers.Add(FlagsNotSampled.ToString());


            setter(carrier, JaegerHeader, string.Join(Separator, headers));
        }

        public override PropagationContext Extract<T>(PropagationContext context, T carrier,
            Func<T, string, IEnumerable<string>> getter)
        {
            if (context.ActivityContext.IsValid() || carrier == null || getter == null)
            {
                return context;
            }

            try
            {
                var traceparentCollection = getter(carrier, JaegerHeader);
                // There must be a single traceparent
                if (traceparentCollection == null || traceparentCollection.Count() != 1)
                {
                    return context;
                }

                var traceparent = traceparentCollection.First();
                var traceparentParsed =
                    TryExtractTraceparent(traceparent, out var traceId, out var spanId, out var traceoptions);

                if (!traceparentParsed)
                {
                    return context;
                }

                return new PropagationContext(
                    new ActivityContext(traceId, spanId, traceoptions, null, isRemote: true),
                    context.Baggage);
            }
            catch
            {
                // ignored
            }

            return context;
        }

        internal static bool TryExtractTraceparent(string traceparent, out ActivityTraceId traceId,
            out ActivitySpanId spanId, out ActivityTraceFlags traceOptions)
        {
            traceId = default;
            spanId = default;
            traceOptions = default;

            var parts = traceparent.Split(Separator);
            if (parts.Length != 4) return false;

            if (!IsTraceIdValid(parts[0])) return false;
            traceId = ActivityTraceId.CreateFromString(parts[0].AsSpan());


            if (!IsSpanIdValid(parts[1])) return false;
            spanId = ActivitySpanId.CreateFromString(parts[1].AsSpan());


            // parts[2] is ignored
            if (!IsFlagsValid(parts[3]) || !int.TryParse(parts[3], out int flags)) return false;
            traceOptions = flags == 1 ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None;

            return true;
        }


        private static bool IsTraceIdValid(string value)
        {
            return !(string.IsNullOrWhiteSpace(value) || (value.Length != TraceId64bitsWidth &&
                     value.Length != TraceId128bitsWidth));
        }

        private static bool IsSpanIdValid(string value)
        {
            return !(string.IsNullOrWhiteSpace(value) || value.Length != SpanIdWidth);
        }

        private static bool IsFlagsValid(string value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }
    }
}
