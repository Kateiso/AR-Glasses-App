using UnityEditor;
public sealed class AirCompanionImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.EndsWith("Companion/Waxiaoxing.png")) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.isReadable = false;
        importer.maxTextureSize = 512;
        importer.textureCompression = TextureImporterCompression.Compressed;
    }
}
