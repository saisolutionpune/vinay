﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;
using UnityEngine;

public class PascalVocGenerator : MonoBehaviour
{

    public GameObject bndBoxPrefab;
    public string captureFolder = "Capture";
    public bool fullPath = false;

    Camera cameraDepth;
    Camera cameraCapture;
    //GameObject temporaryStage;
    GameObject canvas;

    GameObject[] allObjects;
    List<GameObject> allRootObjects;
    List<GameObject> labeledObjects = new List<GameObject>();

    // Exclude bounding boxes that are not in the range of the following area coorinates
    public float boundingBoxOffset = 5F;
    public float minArea = 2000F;

    // Bounding box image coordinates
    class BoundingBox
    {
        public string label;  // "name" element in Pascal VOC XML
        public int xmin;  // "xmin" element
        public int ymin;  // "ymin" element
        public int xmax;  // "xmax" element
        public int ymax;  // "ymax" element

        public BoundingBox(string label, int xmin, int ymin, int xmax, int ymax)
        {
            this.label = label;
            this.xmin = xmin;
            this.ymin = ymin;
            this.xmax = xmax;
            this.ymax = ymax;
        }
    }

    // Find a bounding box
    private bool FindBoundingBox(out int[] coordinates)
    {
        bool found = false;

        // Use the depth camera
        RenderTexture activeRenderTexture = RenderTexture.active;
        RenderTexture.active = cameraDepth.targetTexture;

        cameraDepth.Render();

        Texture2D image = new Texture2D(cameraDepth.targetTexture.width, cameraDepth.targetTexture.height);
        image.ReadPixels(new Rect(0, 0, cameraDepth.targetTexture.width, cameraDepth.targetTexture.height), 0, 0);
        image.Apply();
        RenderTexture.active = activeRenderTexture;

        // Initialize bounding box coordinates on the panel
        int left = image.width;
        int top = 0;
        int right = 0;
        int bottom = image.height;

        // Scan the depth image to find a bounding box 
        for (int y = 0; y < image.height; y++)
        {
            for (int x = 0; x < image.width; x++)
            {
                float px = image.GetPixel(x, y).r;  // Sample red strength in the pixel
                if (px > 0)  // The pixel is in the target object
                {
                    found = true;
                    if (x < left) left = x;
                    if (x > right) right = x;
                    if (y > top) top = y;
                    if (y < bottom) bottom = y;
                }
            }
        }

        Destroy(image);

        if (found)
        {
            int area = (right - left) * (top - bottom);

            if (left < boundingBoxOffset || right > (image.width - boundingBoxOffset) || top > (image.height - boundingBoxOffset) || bottom < boundingBoxOffset || area < minArea)
            {
                found = false;
                Debug.Log($"[This bounding box is discarded] left: {left}, top: {top}, right: {right}, bottom: {bottom}");
            }
            else
            {
                Debug.Log($"[BoundingBox] left: {left}, top: {top}, right: {right}, bottom: {bottom}");
                // Draw a bounding box on the canvas
                GameObject bndBox = Instantiate(bndBoxPrefab) as GameObject;
                bndBox.transform.SetParent(canvas.transform, true);
                RectTransform rectTransform = bndBox.GetComponent<RectTransform>();
                rectTransform.position = new Vector3(left, bottom, 0);
                rectTransform.sizeDelta = new Vector2(right - left, top - bottom);
            }
        }

        // Return the bounding box coordinates
        coordinates = new int[]{ left, top, right, bottom };

        return found;
    }

    private string saveFolder()
    {
        string path = captureFolder;
        if (!fullPath) path = $"{Application.dataPath}/Capture";
        return path;
    }

    private void CaptureImage(string timestamp)
    {
        // Use the depth camera
        RenderTexture activeRenderTexture = RenderTexture.active;
        RenderTexture.active = cameraCapture.targetTexture;

        cameraCapture.Render();

        Texture2D image = new Texture2D(cameraCapture.targetTexture.width, cameraCapture.targetTexture.height);
        image.ReadPixels(new Rect(0, 0, cameraCapture.targetTexture.width, cameraCapture.targetTexture.height), 0, 0);
        image.Apply();
        RenderTexture.active = activeRenderTexture;

        // Convert the image into JPEG format
        byte[] bytes = image.EncodeToJPG();

        Destroy(image);

        File.WriteAllBytes($"{saveFolder()}/{timestamp}.jpg", bytes);
    }

    private void GeneratePascalVOC(string timestamp, List<BoundingBox> boundingBoxes)
    {

        // Create annotation element
        XElement annotation = new XElement("annotation");
        annotation.Add(new XElement("folder", "Capture"));
        annotation.Add(new XElement("filename", $"{timestamp}.jpg"));
        annotation.Add(new XElement("path", $"{Application.dataPath}/Capture/{timestamp}.jpg"));
        annotation.Add(new XElement("source",
                            new XElement("database", "Unknown")
                       ));
        annotation.Add(new XElement("size",
                            new XElement("width", 1024),
                            new XElement("height", 576),
                            new XElement("depth", 3)
                       ));
        annotation.Add(new XElement("segmented", 0));

        // Add bounding boxes
        foreach (BoundingBox bndBox in boundingBoxes)
        {
            annotation.Add(
                new XElement("object",
                new XElement("name", bndBox.label),
                new XElement("pose", "Unspecified"),
                new XElement("truncated", 0),
                new XElement("difficult", 0),
                new XElement("bndbox",
                    new XElement("xmin", bndBox.xmin),
                    new XElement("ymin", bndBox.ymin),
                    new XElement("xmax", bndBox.xmax),
                    new XElement("ymax", bndBox.ymax)
                )
            ));

        }

        // Save it
        string annotationXml = annotation.ToString();
        string pascalVoxXmlFilename = $"{Application.dataPath}/Capture/{timestamp}.xml";
        string path = captureFolder;
        if (!fullPath) path = $"{Application.dataPath}/Capture";
        File.WriteAllBytes($"{saveFolder()}/{timestamp}.xml", Encoding.ASCII.GetBytes(annotationXml));

    }

    // Start is called before the first frame update
    void Start()
    {
        canvas = GameObject.FindWithTag("Canvas");
        allObjects = FindObjectsOfType<GameObject>();
        allRootObjects = new List<GameObject>();

        cameraDepth = GameObject.FindWithTag("CameraDepth").GetComponent<Camera>();
        cameraCapture = GameObject.FindWithTag("CameraCapture").GetComponent<Camera>();
        //temporaryStage = GameObject.FindWithTag("TemporaryStage");

        // List up all the root objects
        foreach (GameObject obj in allObjects)
        {
            if (obj.transform.parent == null) allRootObjects.Add(obj);
        }

        // Find objects that will be bounded and labeled later on.
        foreach (GameObject obj in allRootObjects)
        {
            if (obj.tag.StartsWith("name:"))
            {
                labeledObjects.Add(obj);
            }
        }

    }

    // Update is called once per frame
    void Update()
    {
        List<BoundingBox> boundingBoxes = new List<BoundingBox>();

        if (Input.GetKeyUp(KeyCode.B))
        {
            bool foundAtLeastOne = false;

            // Remove panels that was used before
            for (int i = 0; i < canvas.transform.childCount; i++)
            {
                Destroy(canvas.transform.GetChild(i).gameObject);
            }

            foreach (GameObject targetObj in labeledObjects)
            {
                // Activate the temporary stage so that target objects do not fall down due to the gravity
                //temporaryStage.SetActive(true);

                // Deactivate objects neither the target object nor Studio objects
                foreach (GameObject obj in allRootObjects)
                {
                    if (obj != targetObj && obj.tag != "Studio") obj.SetActive(false);
                }

                // Find a bounding box
                int[] coordinates;

                if (FindBoundingBox(out coordinates))
                {
                    foundAtLeastOne = true;

                    // Translate the panel coordinates into the image coordinates
                    int xmin = coordinates[0];  // left
                    int ymin = 576 - coordinates[1];  // 576 - top 
                    int xmax = coordinates[2];  // right
                    int ymax = 576 - coordinates[3];  // 576 - bottom

                    Debug.Log(targetObj.tag.Split(':'));
                    string name = targetObj.tag.Split(':')[1];

                    boundingBoxes.Add(new BoundingBox(name, xmin, ymin, xmax, ymax));
                }

                // Re-activate objects
                foreach (GameObject obj in allObjects)
                {
                    if (obj != targetObj && obj.tag != "Studio") obj.SetActive(true);
                }

                // Deactivate the temporary stage
                //temporaryStage.SetActive(false);
            }

            if (foundAtLeastOne) {
                // Generate Pascal VOC XML and save it
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                CaptureImage(timestamp);
                GeneratePascalVOC(timestamp, boundingBoxes);
            }
        }
    }
}
