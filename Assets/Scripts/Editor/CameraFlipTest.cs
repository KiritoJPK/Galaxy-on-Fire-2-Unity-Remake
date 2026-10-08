using UnityEditor;
using UnityEngine;

public static class CameraFlipTest
{
    static Vector3? original;   // posição antes do primeiro teste

    [MenuItem("GoF2/Teste Camera/X")]       static void X()   => Flip(-1,  1,  1);
    [MenuItem("GoF2/Teste Camera/Y")]       static void Y()   => Flip( 1, -1,  1);
    [MenuItem("GoF2/Teste Camera/Z")]       static void Z()   => Flip( 1,  1, -1);
    [MenuItem("GoF2/Teste Camera/X + Y")]   static void XY()  => Flip(-1, -1,  1);
    [MenuItem("GoF2/Teste Camera/X + Z")]   static void XZ()  => Flip(-1,  1, -1);
    [MenuItem("GoF2/Teste Camera/Y + Z")]   static void YZ()  => Flip( 1, -1, -1);
    [MenuItem("GoF2/Teste Camera/X + Y + Z")] static void XYZ() => Flip(-1, -1, -1);
    [MenuItem("GoF2/Teste Camera/Voltar ao original")] static void Reset() => Flip(1, 1, 1);

    static void Flip(float x, float y, float z)
    {
        var cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        var alvo = Selection.activeTransform;
        if (cam == null || alvo == null) { Debug.LogWarning("Selecione na Hierarchy o objeto que a camera olha (a nave)."); return; }
        if (original == null) original = cam.transform.position - alvo.position;
        var rel = Vector3.Scale(original.Value, new Vector3(x, y, z));
        cam.transform.position = alvo.position + rel;
        cam.transform.LookAt(alvo, Vector3.up);
        Debug.Log($"Teste ({x}, {y}, {z}). Camera relativa ao alvo, em unidades do jogo: {rel.x * 20f:0}, {rel.y * 20f:0}, {-rel.z * 20f:0}");
    }
	    [MenuItem("GoF2/Teste Camera/Ativar espelho continuo")]
    static void Continuo()
    {
        var cam = Camera.main != null ? Camera.main : Object.FindFirstObjectByType<Camera>();
        var alvo = Selection.activeTransform;
        if (cam == null || alvo == null) { Debug.LogWarning("Selecione a nave na Hierarchy primeiro."); return; }
        var c = cam.GetComponent<CameraMirrorTest>();
        if (c == null) c = cam.gameObject.AddComponent<CameraMirrorTest>();
        c.alvo = alvo;
        Selection.activeGameObject = cam.gameObject;   // mostra as caixinhas no Inspector
    }
}