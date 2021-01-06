using System;
using System.Reflection;
using Unbreakable.Runtime;
using Xunit;
using Xunit.Abstractions;

namespace Unbreakable.Tests {
    public class TimeGuardTests {
        private readonly ITestOutputHelper _output;

        public TimeGuardTests(ITestOutputHelper output) {
            _output = output;
        }

        [Fact]
        public void Allows_TimeoutEqualTo_TimeSpanMaxValue() {
            var m = TestHelper.RewriteAndGetMethodWrappedInScope(
                @"class C { int M() => 1; }", "C", "M",
                runtimeGuardSettings: new RuntimeGuardSettings { TimeLimit = TimeSpan.MaxValue }
            );

            Assert.Equal(1, m());
        }

        [Fact]
        public void HandlesAsyncMethods() {
            var m = TestHelper.RewriteAndGetMethodWrappedInScope(@"
                using System.Threading.Tasks;

                class Program {
                    void M() => Main().Wait(200);

                    async Task Main() {
                        await Task.Delay(100);
                    }
                }
            ", "Program", "M", new AssemblyGuardSettings {
                ApiPolicy = ApiPolicy.SafeDefault()
                    .Namespace("System.Runtime.CompilerServices", ApiAccess.Allowed)
                    .Namespace("System.Threading.Tasks", ApiAccess.Allowed)
            },
            new RuntimeGuardSettings {
                TimeLimit = TimeSpan.FromMilliseconds(100),
            });
            var ex = Assert.Throws<TargetInvocationException>(() => m());
            Assert.IsType<AggregateException>(ex.InnerException);
            Assert.IsType<TimeGuardException>(ex.InnerException?.InnerException);
        }
    }
}
