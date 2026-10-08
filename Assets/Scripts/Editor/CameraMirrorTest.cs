using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(32000)]   // roda depois do script da cutscene
public class CameraMirrorTest : MonoBehaviour
{
    public Transform alvo;            // a nave que a câmera mostra
    public bool espelharX, espelharY, espelharZ;

    Vector3 savedPos; Quaternion savedRot; bool mirrored;

    void OnEnable()  => RenderPipelineManager.endCameraRendering += Restore;
    void OnDisable() { RenderPipelineManager.endCameraRendering -= Restore; RestoreNow(); }

    void LateUpdate()
    {
        if (alvo == null) return;
        var m = new Vector3(espelharX ? -1 : 1, espelharY ? -1 : 1, espelharZ ? -1 : 1);
        if (m == Vector3.one) return;
        savedPos = transform.position; savedRot = transform.rotation;
        transform.position = alvo.position + Vector3.Scale(savedPos - alvo.position, m);
        var frente = Vector3.Scale(savedRot * Vector3.forward, m);
        var cima = Vector3.Scale(savedRot * Vector3.up, new Vector3(m.x, 1, m.z));
        transform.rotation = Quaternion.LookRotation(frente, cima);
        mirrored = true;
    }

    void Restore(ScriptableRenderContext ctx, Camera cam) { if (cam.transform == transform) RestoreNow(); }

    void RestoreNow()
    {
        if (!mirrored) return;
        transform.SetPositionAndRotation(savedPos, savedRot);   // o script da cutscene continua de onde estava
        mirrored = false;
    }
}