
using UnityEngine;
using UnityEngine.Apple;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using static UnityEditor.ShaderData;

public class RaymarchDepthPass : CustomPass
{
    public Material depthMaterial;
    public RenderTexture depthRT;

    protected override void Execute(CustomPassContext ctx)
    {
        if (depthMaterial == null || depthRT == null)
            return;

        ctx.propertyBlock.SetTexture("_RaymarchDepthTex", depthRT);
        CoreUtils.DrawFullScreen(ctx.cmd, depthMaterial, ctx.propertyBlock);
    }
}
