using UnityEngine;

public class MaterialTester : MonoBehaviour
{
    private Material material;
    private void Awake()
    {
        material = GetComponent<MeshRenderer>().material;
    }

    private void Start()
    {
        //var pnInt = material.GetPropertyNames(MaterialPropertyType.Int);
        //var pnFloat = material.GetPropertyNames(MaterialPropertyType.Float);
        //var pnVector = material.GetPropertyNames(MaterialPropertyType.Vector);
        //var pnConstantBuffer = material.GetPropertyNames(MaterialPropertyType.ConstantBuffer);
        //var pnTexture = material.GetPropertyNames(MaterialPropertyType.Texture);
        //var pnComputeBuffer = material.GetPropertyNames(MaterialPropertyType.ComputeBuffer);
        //var pnMatrix = material.GetPropertyNames(MaterialPropertyType.Matrix);
        //var texpn = material.GetTexturePropertyNames();

        //Debug.Log("pnInts");
        //foreach (var pnIn in pnInt) {
        //    Debug.Log($"{pnIn}");
        //}
        //Debug.Log("pnFloats");
        //foreach (var pnFloa in pnFloat) {
        //    Debug.Log($"{pnFloa}");
        //}
        //Debug.Log("pnVectors");
        //foreach (var pnVecto in pnVector) {
        //    Debug.Log($"{pnVecto}");
        //}
        //Debug.Log("pnConstantBuffers");
        //foreach (var pnConstantBuffe in pnConstantBuffer) {
        //    Debug.Log($"{pnConstantBuffe}");
        //}
        //Debug.Log("pnTextures");
        //foreach (var pnTextur in pnTexture) {
        //    Debug.Log($"{pnTextur}");
        //}
        //Debug.Log("pnComputeBuffers");
        //foreach (var pnComputeBuffe in pnComputeBuffer) {
        //    Debug.Log($"{pnComputeBuffe}");
        //}
        //Debug.Log("pnMatrixs");
        //foreach (var pnMatri in pnMatrix) {
        //    Debug.Log($"{pnMatri}");
        //}
        //Debug.Log("texpns");
        //foreach (var texp in texpn) {
        //    Debug.Log($"{texp}");
        //}

    }
    }
