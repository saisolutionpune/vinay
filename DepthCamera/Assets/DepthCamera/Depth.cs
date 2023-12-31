﻿using System;
using System.IO;
using UnityEngine;

public class Depth : MonoBehaviour
{
    public Material depthShader;

    // Capture an image from a camera
    void Capture()
    {
        Camera camera = GetComponent<Camera>();
        RenderTexture activeRenderTexture = RenderTexture.active;
        RenderTexture.active = camera.targetTexture;

        camera.Render();

        try
        {
            Texture2D image = new Texture2D(camera.targetTexture.width, camera.targetTexture.height);
            image.ReadPixels(new Rect(0, 0, camera.targetTexture.width, camera.targetTexture.height), 0, 0);
            image.Apply();
            RenderTexture.active = activeRenderTexture;

            byte[] bytes = image.EncodeToJPG();
            Destroy(image);
            File.WriteAllBytes($"{Application.dataPath}/Capture/depth.jpg", bytes);
        } catch (NullReferenceException e)
        {
        }
    }

    void Start()
    {
        GetComponent<Camera>().depthTextureMode |= DepthTextureMode.Depth;
    }

    void OnRenderImage(RenderTexture source, RenderTexture dest)
    {
        Graphics.Blit(source, dest, depthShader);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C)) Capture();
    }
}