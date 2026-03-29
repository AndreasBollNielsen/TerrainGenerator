
using UnityEngine;
using UnityEngine.Apple;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using static UnityEditor.ShaderData;

public class RaymarchPass : CustomPass
{
    public RenderTexture source;
    public Material blitMaterial;

    protected override void Execute(CustomPassContext ctx)
    {
        if (source == null || blitMaterial == null)
            return;

        blitMaterial.SetTexture("_UnlitColorMap", source);
        CoreUtils.DrawFullScreen(ctx.cmd, blitMaterial, shaderPassId: 0);

        // ctx.cmd.ClearRenderTarget(true, true, Color.magenta);

    }
}
