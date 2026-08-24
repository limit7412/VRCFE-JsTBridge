using nadena.dev.ndmf;
using FEJsTBridge.UseCase;

[assembly: ExportsPlugin(typeof(FEJsTBridge.Handler.FEJsTBridgePlugin))]

namespace FEJsTBridge.Handler
{
    /// <summary>
    /// NDMFビルドのエントリポイント
    /// 生成処理はUseCase層へ委譲する
    /// </summary>
    public class FEJsTBridgePlugin : Plugin<FEJsTBridgePlugin>
    {
        public override string QualifiedName => "com.qazx7412.kx-vrc-fe-jst-bridge";
        public override string DisplayName => "Kx VRC FE-JsT Bridge";

        protected override void Configure()
        {
            // 生成したMerge AnimatorをModular Avatarが処理する前に配置し終える必要がある。
            // Jerry's Templates本体との順序制約は不要で、ブリッジはJerryのアセットを読まず、
            // マージ後のFXでパラメータ名が一致すればよい
            InPhase(BuildPhase.Generating)
                .BeforePlugin("nadena.dev.modular-avatar")
                .Run("Generate FaceEmo bypass bridge", GenerateBridgeUseCase.ExecuteForBuild);
        }
    }
}
