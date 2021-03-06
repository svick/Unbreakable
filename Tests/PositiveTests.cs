using Xunit;

namespace Unbreakable.Tests {
    public class PositiveTests {
        [Theory]
        [InlineData(@"string M(int a) => ""x"" + a.ToString();", "x1")]
        [InlineData(@"int M(int a) => (new[] { a, 2, 3 })[1];", 2)]
        [InlineData(@"
            delegate int F();
            int M(int a) => ((F)(() => a + 1))();
        ", 2)]
        [InlineData(@"bool M(int a) => DateTime.Now.Ticks > 0;", true)] // crash, found by Valery Sarkisov‏ (@VSarkisov)
        [InlineData(@"int M(int _) { MOut(out int x); return x; } void MOut(out int b) { b = 1; }", 1)]
        public void PreservesStandardLogic(string code, object expected) {
            var m = TestHelper.RewriteAndGetMethodWrappedInScope(@"
                using System;
                class C {
                    " + code + @"
                }
            ", "C", "M");
            Assert.Equal(expected, m(1));
        }

        [Theory]
        [InlineData("byte M() => System.Text.Encoding.UTF8.GetBytes(\"a\")[0];", (byte)97)]
        [InlineData("string M() => System.Text.Encoding.UTF8.GetString(new byte[] { 97 });", "a")]
        public void HandlesStandardApis(string code, object expected) {
            var m = TestHelper.RewriteAndGetMethodWrappedInScope(@"
                using System;
                class C {
                    " + code + @"
                }
            ", "C", "M");
            Assert.Equal(expected, m());
        }

        [Theory]
        [InlineData("int M() { ref T G<T>(ref T a) => ref a; var x = 1; return G(ref x); }", 1)]
        [InlineData("int M() { var span = new SpanStub<int>(new int[1]); return span[0]; }", 0)]
        public void HandlesGenericReferenceTypes(string code, int expected) {
            var m = TestHelper.RewriteAndGetMethodWrappedInScope(@"
                using System;
                using Unbreakable.Tests.Internal;
                class C {
                    " + code + @"
                }
            ", "C", "M", new AssemblyGuardSettings {
                ApiPolicy = ApiPolicy.SafeDefault().Namespace("Unbreakable.Tests.Internal", ApiAccess.Allowed)
            });
            Assert.Equal(expected, m());
        }

        [Fact]
        public void AllowsTypesInEmptyNamespace_IfAllowed() {
            var m = TestHelper.RewriteAndGetMethodWrappedInScope(@"
                using System;
                class C {
                    public object M() {
                        return new " + nameof(TestClassWithoutNamespace) + @"();
                    }
                }
            ", "C", "M", new AssemblyGuardSettings {
                ApiPolicy = ApiPolicy.SafeDefault().Namespace("", ApiAccess.Allowed)
            });
            Assert.NotNull(m());
        }

        [Fact]
        public void HandlesAsyncMethods() {
            var m = TestHelper.RewriteAndGetMethodWrappedInScope(@"
                using System.Threading.Tasks;

                class Program {
                    bool M() => Main().Wait(100);

                    async Task Main() {
                        await Task.Yield();
                    }
                }
            ", "Program", "M", new AssemblyGuardSettings {
                ApiPolicy = ApiPolicy.SafeDefault()
                    .Namespace("System.Runtime.CompilerServices", ApiAccess.Allowed)
                    .Namespace("System.Threading.Tasks", ApiAccess.Allowed)
            });
            Assert.True(m() is true);
        }

        [Fact]
        public void ComputesIlOffsetsCorrectly() {
            var m = TestHelper.RewriteAndGetMethodWrappedInScope(@"
                class Program {
                    void M() {
                        bool cond = true;
                        if (cond)
                        {
                            M2();
                            // produce as many nops as necessary
                            ;;;;;;;;;; ;;;;;;;;;;
                            ;;;;;;;;;; ;;;;;;;;;;
                            ;;;;;;;;;; ;;;;;;;;;;
                            ;;;;;;;;;; ;;;;;;;;;;
                            ;;;;;;;;;; ;;;;;;;;;;
                            ;;;;;;;;;; ;;;
                        }
                    }
                    void M2() {}
                }
            ", "Program", "M");
            m();
        }

        [Fact]
        public void ComputesIlOffsetsCorrectly2() {
            var m = TestHelper.RewriteAndGetMethodWrappedInScope(@"
                class Program {
                    void M() {
                        bool cond = true;
                        if (cond)
                            goto l1;
                        if (cond)
                            goto l2;
                        M2();
                        // produce as many nops as necessary
                        ;;;;;;;;;; ;;;;;;;;;;
                        ;;;;;;;;;; ;;;;;;;;;;
                        ;;;;;;;;;; ;;;;;;;;;;
                        ;;;;;;;;;; ;;;;;;;;;;
                        ;;;;;;;;;; ;;;;;;;;;;
                        ;;;;;;;
                    l1: ;;;;;;;
                    l2: ;
                    }
                    void M2() {}
                }
            ", "Program", "M");
            m();
        }
    }
}
