using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Runtime
{
    /// <summary>A sprite snapshot that finishes dying after the gameplay enemy returns to its pool.</summary>
    public sealed class EnemyDeathVisual : MonoBehaviour
    {
        private float duration;
        private float elapsed;

        public static void Play(SpriteRenderer source, float seconds)
        {
            if (source == null || source.sprite == null || !source.enabled) return;
            var root = new GameObject("Enemy shrink away");
            SceneManager.MoveGameObjectToScene(root, source.gameObject.scene);
            var art = source.sprite;
            float bottom = source.flipY ? -art.bounds.max.y : art.bounds.min.y;
            root.transform.position = source.transform.TransformPoint(new Vector3(0f, bottom, 0f));
            var image = new GameObject("Dying sprite");
            image.layer = source.gameObject.layer;
            image.transform.SetParent(root.transform, false);
            image.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            image.transform.localScale = source.transform.lossyScale;
            var sprite = image.AddComponent<SpriteRenderer>();
            sprite.sprite = art;
            sprite.sharedMaterial = source.sharedMaterial;
            sprite.color = source.color;
            sprite.flipX = source.flipX;
            sprite.flipY = source.flipY;
            sprite.sortingLayerID = source.sortingLayerID;
            sprite.sortingOrder = source.sortingOrder;
            sprite.spriteSortPoint = source.spriteSortPoint;
            var properties = new MaterialPropertyBlock();
            source.GetPropertyBlock(properties);
            sprite.SetPropertyBlock(properties);
            root.AddComponent<EnemyDeathVisual>().duration = Mathf.Max(0.01f, seconds);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.localScale = Vector3.one * (1f - Mathf.SmoothStep(0f, 1f, t));
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
