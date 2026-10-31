extern "C" void RegisterStaticallyLinkedModulesGranular()
{
	void RegisterModule_SharedInternals();
	RegisterModule_SharedInternals();

	void RegisterModule_Core();
	RegisterModule_Core();

	void RegisterModule_AI();
	RegisterModule_AI();

	void RegisterModule_Animation();
	RegisterModule_Animation();

	void RegisterModule_Audio();
	RegisterModule_Audio();

	void RegisterModule_CrashReporting();
	RegisterModule_CrashReporting();

	void RegisterModule_GameCenter();
	RegisterModule_GameCenter();

	void RegisterModule_GraphicsStateCollectionSerializer();
	RegisterModule_GraphicsStateCollectionSerializer();

	void RegisterModule_HierarchyCore();
	RegisterModule_HierarchyCore();

	void RegisterModule_HotReload();
	RegisterModule_HotReload();

	void RegisterModule_AssetBundle();
	RegisterModule_AssetBundle();

	void RegisterModule_InputLegacy();
	RegisterModule_InputLegacy();

	void RegisterModule_IMGUI();
	RegisterModule_IMGUI();

	void RegisterModule_Identifiers();
	RegisterModule_Identifiers();

	void RegisterModule_ImageConversion();
	RegisterModule_ImageConversion();

	void RegisterModule_Input();
	RegisterModule_Input();

	void RegisterModule_InputForUI();
	RegisterModule_InputForUI();

	void RegisterModule_JSONSerialize();
	RegisterModule_JSONSerialize();

	void RegisterModule_Insights();
	RegisterModule_Insights();

	void RegisterModule_ParticleSystem();
	RegisterModule_ParticleSystem();

	void RegisterModule_Physics();
	RegisterModule_Physics();

	void RegisterModule_Physics2D();
	RegisterModule_Physics2D();

	void RegisterModule_PhysicsBackendPhysX();
	RegisterModule_PhysicsBackendPhysX();

	void RegisterModule_Properties();
	RegisterModule_Properties();

	void RegisterModule_RuntimeInitializeOnLoadManagerInitializer();
	RegisterModule_RuntimeInitializeOnLoadManagerInitializer();

	void RegisterModule_Subsystems();
	RegisterModule_Subsystems();

	void RegisterModule_TLS();
	RegisterModule_TLS();

	void RegisterModule_Terrain();
	RegisterModule_Terrain();

	void RegisterModule_TextRendering();
	RegisterModule_TextRendering();

	void RegisterModule_TextCoreFontEngine();
	RegisterModule_TextCoreFontEngine();

	void RegisterModule_TextCoreTextEngine();
	RegisterModule_TextCoreTextEngine();

	void RegisterModule_UI();
	RegisterModule_UI();

	void RegisterModule_UIElements();
	RegisterModule_UIElements();

	void RegisterModule_UnityAnalyticsCommon();
	RegisterModule_UnityAnalyticsCommon();

	void RegisterModule_UnityConnect();
	RegisterModule_UnityConnect();

	void RegisterModule_UnityAnalytics();
	RegisterModule_UnityAnalytics();

	void RegisterModule_UnityConsent();
	RegisterModule_UnityConsent();

	void RegisterModule_UnityWebRequest();
	RegisterModule_UnityWebRequest();

	void RegisterModule_VFX();
	RegisterModule_VFX();

	void RegisterModule_VR();
	RegisterModule_VR();

	void RegisterModule_Video();
	RegisterModule_Video();

	void RegisterModule_XR();
	RegisterModule_XR();

}

template <typename T> void RegisterUnityClass(const char*);
template <typename T> void RegisterStrippedType(int, const char*, const char*);

void InvokeRegisterStaticallyLinkedModuleClasses()
{
	// Do nothing (we're in stripping mode)
}

class NavMeshAgent; template <> void RegisterUnityClass<NavMeshAgent>(const char*);
class NavMeshData; template <> void RegisterUnityClass<NavMeshData>(const char*);
class NavMeshProjectSettings; template <> void RegisterUnityClass<NavMeshProjectSettings>(const char*);
class NavMeshSettings; template <> void RegisterUnityClass<NavMeshSettings>(const char*);
class Animation; template <> void RegisterUnityClass<Animation>(const char*);
class AnimationClip; template <> void RegisterUnityClass<AnimationClip>(const char*);
class Animator; template <> void RegisterUnityClass<Animator>(const char*);
class AnimatorController; template <> void RegisterUnityClass<AnimatorController>(const char*);
class AnimatorOverrideController; template <> void RegisterUnityClass<AnimatorOverrideController>(const char*);
class Avatar; template <> void RegisterUnityClass<Avatar>(const char*);
class Motion; template <> void RegisterUnityClass<Motion>(const char*);
class RuntimeAnimatorController; template <> void RegisterUnityClass<RuntimeAnimatorController>(const char*);
class AssetBundle; template <> void RegisterUnityClass<AssetBundle>(const char*);
class AudioBehaviour; template <> void RegisterUnityClass<AudioBehaviour>(const char*);
class AudioClip; template <> void RegisterUnityClass<AudioClip>(const char*);
class AudioFilter; template <> void RegisterUnityClass<AudioFilter>(const char*);
class AudioListener; template <> void RegisterUnityClass<AudioListener>(const char*);
class AudioLowPassFilter; template <> void RegisterUnityClass<AudioLowPassFilter>(const char*);
class AudioManager; template <> void RegisterUnityClass<AudioManager>(const char*);
class AudioResource; template <> void RegisterUnityClass<AudioResource>(const char*);
class AudioSource; template <> void RegisterUnityClass<AudioSource>(const char*);
class SampleClip; template <> void RegisterUnityClass<SampleClip>(const char*);
class Behaviour; template <> void RegisterUnityClass<Behaviour>(const char*);
class BuildSettings; template <> void RegisterUnityClass<BuildSettings>(const char*);
class Camera; template <> void RegisterUnityClass<Camera>(const char*);
namespace Unity { class Component; } template <> void RegisterUnityClass<Unity::Component>(const char*);
class ComputeShader; template <> void RegisterUnityClass<ComputeShader>(const char*);
class Cubemap; template <> void RegisterUnityClass<Cubemap>(const char*);
class CubemapArray; template <> void RegisterUnityClass<CubemapArray>(const char*);
class DelayedCallManager; template <> void RegisterUnityClass<DelayedCallManager>(const char*);
class EditorExtension; template <> void RegisterUnityClass<EditorExtension>(const char*);
class GameManager; template <> void RegisterUnityClass<GameManager>(const char*);
class GameObject; template <> void RegisterUnityClass<GameObject>(const char*);
class GlobalGameManager; template <> void RegisterUnityClass<GlobalGameManager>(const char*);
class GraphicsSettings; template <> void RegisterUnityClass<GraphicsSettings>(const char*);
class InputManager; template <> void RegisterUnityClass<InputManager>(const char*);
class LODGroup; template <> void RegisterUnityClass<LODGroup>(const char*);
class LevelGameManager; template <> void RegisterUnityClass<LevelGameManager>(const char*);
class Light; template <> void RegisterUnityClass<Light>(const char*);
class LightProbeProxyVolume; template <> void RegisterUnityClass<LightProbeProxyVolume>(const char*);
class LightProbes; template <> void RegisterUnityClass<LightProbes>(const char*);
class LightingSettings; template <> void RegisterUnityClass<LightingSettings>(const char*);
class LightmapSettings; template <> void RegisterUnityClass<LightmapSettings>(const char*);
class LineRenderer; template <> void RegisterUnityClass<LineRenderer>(const char*);
class LowerResBlitTexture; template <> void RegisterUnityClass<LowerResBlitTexture>(const char*);
class Material; template <> void RegisterUnityClass<Material>(const char*);
class Mesh; template <> void RegisterUnityClass<Mesh>(const char*);
class MeshFilter; template <> void RegisterUnityClass<MeshFilter>(const char*);
class MeshRenderer; template <> void RegisterUnityClass<MeshRenderer>(const char*);
class MonoBehaviour; template <> void RegisterUnityClass<MonoBehaviour>(const char*);
class MonoManager; template <> void RegisterUnityClass<MonoManager>(const char*);
class MonoScript; template <> void RegisterUnityClass<MonoScript>(const char*);
class NamedObject; template <> void RegisterUnityClass<NamedObject>(const char*);
class Object; template <> void RegisterUnityClass<Object>(const char*);
class PlayerSettings; template <> void RegisterUnityClass<PlayerSettings>(const char*);
class PreloadData; template <> void RegisterUnityClass<PreloadData>(const char*);
class QualitySettings; template <> void RegisterUnityClass<QualitySettings>(const char*);
class RayTracingShader; template <> void RegisterUnityClass<RayTracingShader>(const char*);
namespace UI { class RectTransform; } template <> void RegisterUnityClass<UI::RectTransform>(const char*);
class ReflectionProbe; template <> void RegisterUnityClass<ReflectionProbe>(const char*);
class RenderSettings; template <> void RegisterUnityClass<RenderSettings>(const char*);
class RenderTexture; template <> void RegisterUnityClass<RenderTexture>(const char*);
class Renderer; template <> void RegisterUnityClass<Renderer>(const char*);
class ResourceManager; template <> void RegisterUnityClass<ResourceManager>(const char*);
class RuntimeInitializeOnLoadManager; template <> void RegisterUnityClass<RuntimeInitializeOnLoadManager>(const char*);
class Shader; template <> void RegisterUnityClass<Shader>(const char*);
class ShaderNameRegistry; template <> void RegisterUnityClass<ShaderNameRegistry>(const char*);
class SkinnedMeshRenderer; template <> void RegisterUnityClass<SkinnedMeshRenderer>(const char*);
class Skybox; template <> void RegisterUnityClass<Skybox>(const char*);
class SortingGroup; template <> void RegisterUnityClass<SortingGroup>(const char*);
class Sprite; template <> void RegisterUnityClass<Sprite>(const char*);
class SpriteAtlas; template <> void RegisterUnityClass<SpriteAtlas>(const char*);
class SpriteRenderer; template <> void RegisterUnityClass<SpriteRenderer>(const char*);
class TagManager; template <> void RegisterUnityClass<TagManager>(const char*);
class TextAsset; template <> void RegisterUnityClass<TextAsset>(const char*);
class Texture; template <> void RegisterUnityClass<Texture>(const char*);
class Texture2D; template <> void RegisterUnityClass<Texture2D>(const char*);
class Texture2DArray; template <> void RegisterUnityClass<Texture2DArray>(const char*);
class Texture3D; template <> void RegisterUnityClass<Texture3D>(const char*);
class TimeManager; template <> void RegisterUnityClass<TimeManager>(const char*);
class Transform; template <> void RegisterUnityClass<Transform>(const char*);
class ParticleSystem; template <> void RegisterUnityClass<ParticleSystem>(const char*);
class ParticleSystemRenderer; template <> void RegisterUnityClass<ParticleSystemRenderer>(const char*);
class BoxCollider; template <> void RegisterUnityClass<BoxCollider>(const char*);
class CapsuleCollider; template <> void RegisterUnityClass<CapsuleCollider>(const char*);
class CharacterController; template <> void RegisterUnityClass<CharacterController>(const char*);
namespace Unity { class CharacterJoint; } template <> void RegisterUnityClass<Unity::CharacterJoint>(const char*);
class Collider; template <> void RegisterUnityClass<Collider>(const char*);
namespace Unity { class FixedJoint; } template <> void RegisterUnityClass<Unity::FixedJoint>(const char*);
namespace Unity { class Joint; } template <> void RegisterUnityClass<Unity::Joint>(const char*);
class MeshCollider; template <> void RegisterUnityClass<MeshCollider>(const char*);
class PhysicsManager; template <> void RegisterUnityClass<PhysicsManager>(const char*);
class Rigidbody; template <> void RegisterUnityClass<Rigidbody>(const char*);
class SphereCollider; template <> void RegisterUnityClass<SphereCollider>(const char*);
class AnchoredJoint2D; template <> void RegisterUnityClass<AnchoredJoint2D>(const char*);
class FixedJoint2D; template <> void RegisterUnityClass<FixedJoint2D>(const char*);
class Joint2D; template <> void RegisterUnityClass<Joint2D>(const char*);
class Physics2DSettings; template <> void RegisterUnityClass<Physics2DSettings>(const char*);
class Rigidbody2D; template <> void RegisterUnityClass<Rigidbody2D>(const char*);
class Terrain; template <> void RegisterUnityClass<Terrain>(const char*);
class TerrainData; template <> void RegisterUnityClass<TerrainData>(const char*);
namespace TextRendering { class Font; } template <> void RegisterUnityClass<TextRendering::Font>(const char*);
namespace UI { class Canvas; } template <> void RegisterUnityClass<UI::Canvas>(const char*);
namespace UI { class CanvasGroup; } template <> void RegisterUnityClass<UI::CanvasGroup>(const char*);
namespace UI { class CanvasRenderer; } template <> void RegisterUnityClass<UI::CanvasRenderer>(const char*);
class UIRenderer; template <> void RegisterUnityClass<UIRenderer>(const char*);
class UnityConnectSettings; template <> void RegisterUnityClass<UnityConnectSettings>(const char*);
class VFXManager; template <> void RegisterUnityClass<VFXManager>(const char*);
class VFXRenderer; template <> void RegisterUnityClass<VFXRenderer>(const char*);
class VisualEffect; template <> void RegisterUnityClass<VisualEffect>(const char*);
class VisualEffectAsset; template <> void RegisterUnityClass<VisualEffectAsset>(const char*);
class VisualEffectObject; template <> void RegisterUnityClass<VisualEffectObject>(const char*);
class VideoClip; template <> void RegisterUnityClass<VideoClip>(const char*);
class VideoPlayer; template <> void RegisterUnityClass<VideoPlayer>(const char*);

void RegisterAllClasses()
{
void RegisterBuiltinTypes();
RegisterBuiltinTypes();
	//Total: 114 non stripped classes
	//0. NavMeshAgent
	RegisterUnityClass<NavMeshAgent>("AI");
	//1. NavMeshData
	RegisterUnityClass<NavMeshData>("AI");
	//2. NavMeshProjectSettings
	RegisterUnityClass<NavMeshProjectSettings>("AI");
	//3. NavMeshSettings
	RegisterUnityClass<NavMeshSettings>("AI");
	//4. Animation
	RegisterUnityClass<Animation>("Animation");
	//5. AnimationClip
	RegisterUnityClass<AnimationClip>("Animation");
	//6. Animator
	RegisterUnityClass<Animator>("Animation");
	//7. AnimatorController
	RegisterUnityClass<AnimatorController>("Animation");
	//8. AnimatorOverrideController
	RegisterUnityClass<AnimatorOverrideController>("Animation");
	//9. Avatar
	RegisterUnityClass<Avatar>("Animation");
	//10. Motion
	RegisterUnityClass<Motion>("Animation");
	//11. RuntimeAnimatorController
	RegisterUnityClass<RuntimeAnimatorController>("Animation");
	//12. AssetBundle
	RegisterUnityClass<AssetBundle>("AssetBundle");
	//13. AudioBehaviour
	RegisterUnityClass<AudioBehaviour>("Audio");
	//14. AudioClip
	RegisterUnityClass<AudioClip>("Audio");
	//15. AudioFilter
	RegisterUnityClass<AudioFilter>("Audio");
	//16. AudioListener
	RegisterUnityClass<AudioListener>("Audio");
	//17. AudioLowPassFilter
	RegisterUnityClass<AudioLowPassFilter>("Audio");
	//18. AudioManager
	RegisterUnityClass<AudioManager>("Audio");
	//19. AudioResource
	RegisterUnityClass<AudioResource>("Audio");
	//20. AudioSource
	RegisterUnityClass<AudioSource>("Audio");
	//21. SampleClip
	RegisterUnityClass<SampleClip>("Audio");
	//22. Behaviour
	RegisterUnityClass<Behaviour>("Core");
	//23. BuildSettings
	RegisterUnityClass<BuildSettings>("Core");
	//24. Camera
	RegisterUnityClass<Camera>("Core");
	//25. Component
	RegisterUnityClass<Unity::Component>("Core");
	//26. ComputeShader
	RegisterUnityClass<ComputeShader>("Core");
	//27. Cubemap
	RegisterUnityClass<Cubemap>("Core");
	//28. CubemapArray
	RegisterUnityClass<CubemapArray>("Core");
	//29. DelayedCallManager
	RegisterUnityClass<DelayedCallManager>("Core");
	//30. EditorExtension
	RegisterUnityClass<EditorExtension>("Core");
	//31. GameManager
	RegisterUnityClass<GameManager>("Core");
	//32. GameObject
	RegisterUnityClass<GameObject>("Core");
	//33. GlobalGameManager
	RegisterUnityClass<GlobalGameManager>("Core");
	//34. GraphicsSettings
	RegisterUnityClass<GraphicsSettings>("Core");
	//35. InputManager
	RegisterUnityClass<InputManager>("Core");
	//36. LODGroup
	RegisterUnityClass<LODGroup>("Core");
	//37. LevelGameManager
	RegisterUnityClass<LevelGameManager>("Core");
	//38. Light
	RegisterUnityClass<Light>("Core");
	//39. LightProbeProxyVolume
	RegisterUnityClass<LightProbeProxyVolume>("Core");
	//40. LightProbes
	RegisterUnityClass<LightProbes>("Core");
	//41. LightingSettings
	RegisterUnityClass<LightingSettings>("Core");
	//42. LightmapSettings
	RegisterUnityClass<LightmapSettings>("Core");
	//43. LineRenderer
	RegisterUnityClass<LineRenderer>("Core");
	//44. LowerResBlitTexture
	RegisterUnityClass<LowerResBlitTexture>("Core");
	//45. Material
	RegisterUnityClass<Material>("Core");
	//46. Mesh
	RegisterUnityClass<Mesh>("Core");
	//47. MeshFilter
	RegisterUnityClass<MeshFilter>("Core");
	//48. MeshRenderer
	RegisterUnityClass<MeshRenderer>("Core");
	//49. MonoBehaviour
	RegisterUnityClass<MonoBehaviour>("Core");
	//50. MonoManager
	RegisterUnityClass<MonoManager>("Core");
	//51. MonoScript
	RegisterUnityClass<MonoScript>("Core");
	//52. NamedObject
	RegisterUnityClass<NamedObject>("Core");
	//53. Object
	//Skipping Object
	//54. PlayerSettings
	RegisterUnityClass<PlayerSettings>("Core");
	//55. PreloadData
	RegisterUnityClass<PreloadData>("Core");
	//56. QualitySettings
	RegisterUnityClass<QualitySettings>("Core");
	//57. RayTracingShader
	RegisterUnityClass<RayTracingShader>("Core");
	//58. RectTransform
	RegisterUnityClass<UI::RectTransform>("Core");
	//59. ReflectionProbe
	RegisterUnityClass<ReflectionProbe>("Core");
	//60. RenderSettings
	RegisterUnityClass<RenderSettings>("Core");
	//61. RenderTexture
	RegisterUnityClass<RenderTexture>("Core");
	//62. Renderer
	RegisterUnityClass<Renderer>("Core");
	//63. ResourceManager
	RegisterUnityClass<ResourceManager>("Core");
	//64. RuntimeInitializeOnLoadManager
	RegisterUnityClass<RuntimeInitializeOnLoadManager>("Core");
	//65. Shader
	RegisterUnityClass<Shader>("Core");
	//66. ShaderNameRegistry
	RegisterUnityClass<ShaderNameRegistry>("Core");
	//67. SkinnedMeshRenderer
	RegisterUnityClass<SkinnedMeshRenderer>("Core");
	//68. Skybox
	RegisterUnityClass<Skybox>("Core");
	//69. SortingGroup
	RegisterUnityClass<SortingGroup>("Core");
	//70. Sprite
	RegisterUnityClass<Sprite>("Core");
	//71. SpriteAtlas
	RegisterUnityClass<SpriteAtlas>("Core");
	//72. SpriteRenderer
	RegisterUnityClass<SpriteRenderer>("Core");
	//73. TagManager
	RegisterUnityClass<TagManager>("Core");
	//74. TextAsset
	RegisterUnityClass<TextAsset>("Core");
	//75. Texture
	RegisterUnityClass<Texture>("Core");
	//76. Texture2D
	RegisterUnityClass<Texture2D>("Core");
	//77. Texture2DArray
	RegisterUnityClass<Texture2DArray>("Core");
	//78. Texture3D
	RegisterUnityClass<Texture3D>("Core");
	//79. TimeManager
	RegisterUnityClass<TimeManager>("Core");
	//80. Transform
	RegisterUnityClass<Transform>("Core");
	//81. ParticleSystem
	RegisterUnityClass<ParticleSystem>("ParticleSystem");
	//82. ParticleSystemRenderer
	RegisterUnityClass<ParticleSystemRenderer>("ParticleSystem");
	//83. BoxCollider
	RegisterUnityClass<BoxCollider>("Physics");
	//84. CapsuleCollider
	RegisterUnityClass<CapsuleCollider>("Physics");
	//85. CharacterController
	RegisterUnityClass<CharacterController>("Physics");
	//86. CharacterJoint
	RegisterUnityClass<Unity::CharacterJoint>("Physics");
	//87. Collider
	RegisterUnityClass<Collider>("Physics");
	//88. FixedJoint
	RegisterUnityClass<Unity::FixedJoint>("Physics");
	//89. Joint
	RegisterUnityClass<Unity::Joint>("Physics");
	//90. MeshCollider
	RegisterUnityClass<MeshCollider>("Physics");
	//91. PhysicsManager
	RegisterUnityClass<PhysicsManager>("Physics");
	//92. Rigidbody
	RegisterUnityClass<Rigidbody>("Physics");
	//93. SphereCollider
	RegisterUnityClass<SphereCollider>("Physics");
	//94. AnchoredJoint2D
	RegisterUnityClass<AnchoredJoint2D>("Physics2D");
	//95. FixedJoint2D
	RegisterUnityClass<FixedJoint2D>("Physics2D");
	//96. Joint2D
	RegisterUnityClass<Joint2D>("Physics2D");
	//97. Physics2DSettings
	RegisterUnityClass<Physics2DSettings>("Physics2D");
	//98. Rigidbody2D
	RegisterUnityClass<Rigidbody2D>("Physics2D");
	//99. Terrain
	RegisterUnityClass<Terrain>("Terrain");
	//100. TerrainData
	RegisterUnityClass<TerrainData>("Terrain");
	//101. Font
	RegisterUnityClass<TextRendering::Font>("TextRendering");
	//102. Canvas
	RegisterUnityClass<UI::Canvas>("UI");
	//103. CanvasGroup
	RegisterUnityClass<UI::CanvasGroup>("UI");
	//104. CanvasRenderer
	RegisterUnityClass<UI::CanvasRenderer>("UI");
	//105. UIRenderer
	RegisterUnityClass<UIRenderer>("UIElements");
	//106. UnityConnectSettings
	RegisterUnityClass<UnityConnectSettings>("UnityConnect");
	//107. VFXManager
	RegisterUnityClass<VFXManager>("VFX");
	//108. VFXRenderer
	RegisterUnityClass<VFXRenderer>("VFX");
	//109. VisualEffect
	RegisterUnityClass<VisualEffect>("VFX");
	//110. VisualEffectAsset
	RegisterUnityClass<VisualEffectAsset>("VFX");
	//111. VisualEffectObject
	RegisterUnityClass<VisualEffectObject>("VFX");
	//112. VideoClip
	RegisterUnityClass<VideoClip>("Video");
	//113. VideoPlayer
	RegisterUnityClass<VideoPlayer>("Video");

}
