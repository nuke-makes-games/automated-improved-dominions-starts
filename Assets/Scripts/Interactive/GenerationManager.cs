﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml.Serialization;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manager class that handles all user input and generally is the entry point for a lot of logic.
/// Has a global singleton.
/// </summary>
public class GenerationManager : MonoBehaviour
{
    public static GenerationManager s_generation_manager;

    public Camera CaptureCamera;
    public Toggle NatStarts;
    public AudioClip AcceptAudio;
    public AudioClip ClickAudio;
    public AudioClip DenyAudio;
    public GameObject OutputWindow;
    public GameObject LoadingScreen;
    public InputField MapName;
    public Dropdown LayoutDropdown;
    public GameObject NationPicker;
    public GameObject ScrollContent;
    public GameObject ScrollPanel;
    public GameObject Logo;
    public GameObject LogScreen;
    public GameObject LogContent;
    public GameObject[] HideableOptions;
    public GameObject[] HideableControls;

    bool m_generic_starts = false;
    bool m_cluster_water = true;
    bool m_teamplay = false;
    int m_player_count = 9;
    Age m_age = Age.EARLY;
    Season m_season = Season.SUMMER;
    List<GameObject> m_log_content;
    List<GameObject> m_content;
    List<PlayerData> m_nations;
    NodeLayoutCollection m_layouts;

    public Season Season
    {
        get
        {
            return m_season;
        }
    }

    public List<PlayerData> NationData
    {
        get
        {
            return m_nations;
        }
    }

    void Start()
    {
        AllNationData.Init();
        GeneratorSettings.Initialize();

        s_generation_manager = this;
        m_content = new List<GameObject>();
        m_log_content = new List<GameObject>();

        load_layouts();
        update_nations();
        hide_controls();
    }

    void Update()
    {

    }

    public void LogText(string text)
    {
        StartCoroutine(do_log(text));
    }

    IEnumerator do_log(string text)
    {
        yield return null;

        int pos = m_log_content.Count + 1;

        GameObject obj = GameObject.Instantiate(LogContent);
        RectTransform rt = obj.GetComponent<RectTransform>();
        UnityEngine.UI.Text txt = obj.GetComponent<UnityEngine.UI.Text>();

        txt.text = text;
        rt.SetParent(LogScreen.GetComponent<RectTransform>());
        rt.localPosition = new Vector3(10, 10 - (pos * 20), 0);
        rt.sizeDelta = new Vector2(0, 20);
        rt.anchoredPosition = new Vector2(10, 10 - (pos * 20));

        m_log_content.Add(obj);

        yield return null;
    }

    public void Click()
    {
        GetComponent<AudioSource>().PlayOneShot(ClickAudio);
    }

    public void ClearLog()
    {
        foreach (GameObject g in m_log_content)
        {
            GameObject.Destroy(g);
        }

        m_log_content = new List<GameObject>();
    }

    public void OnGenerate()
    {
        /*if (Logo != null && Logo.activeSelf)
        {
            Logo.SetActive(false);
        }*/

        if (ElementManager.s_element_manager.GeneratedObjects.Any())
        {
            ElementManager.s_element_manager.WipeGeneratedObjects();
        }

        List<PlayerData> picks = new List<PlayerData>();

        foreach (GameObject obj in m_content)
        {
            NationPicker np = obj.GetComponent<NationPicker>();
            string str = np.NationName;

            NationData data = AllNationData.AllNations.FirstOrDefault(x => x.Name == str);
            PlayerData pd = new PlayerData(data, np.TeamNum);

            if (data == null || (picks.Any(x => x.NationData.Name == data.Name) && !m_generic_starts))
            {
                GetComponent<AudioSource>().PlayOneShot(DenyAudio);
                return;
            }

            picks.Add(pd);
        }

        m_nations = picks;

        GetComponent<AudioSource>().PlayOneShot(AcceptAudio);

        StartCoroutine(perform_async(() => do_generate(), true));
    }

    void do_generate() // pipeline for initial generation of all nodes and stuff
    {
        NodeLayout layout = m_layouts.Layouts.FirstOrDefault(x => x.Name == LayoutDropdown.options[LayoutDropdown.value].text && x.NumPlayers == m_player_count);

        if (layout == null)
        {
            layout = m_layouts.Layouts.FirstOrDefault(x => x.NumPlayers == m_player_count);
        }

        m_season = Season.SUMMER;

        // create the conceptual nodes and connections first
        WorldGenerator.GenerateWorld(m_teamplay, m_cluster_water, NatStarts.isOn, m_nations, layout);
        List<Connection> conns = WorldGenerator.GetConnections();
        List<Node> nodes = WorldGenerator.GetNodes();

        // generate the unity objects using the conceptual nodes
        ElementManager mgr = GetComponent<ElementManager>();
        mgr.GenerateElements(nodes, conns, layout);

        ProvinceManager.s_province_manager.SetLayout(layout);
        ConnectionManager.s_connection_manager.SetLayout(layout);

        // position and resize the cameras
        Vector3 campos = new Vector3(layout.X * 0.5f * mgr.X - mgr.X, layout.Y * 0.5f * mgr.Y - mgr.Y, -10);
        Camera.main.transform.position = campos + new Vector3(500f, 0f, 0f);
        CaptureCamera.transform.position = campos;

        float ortho = (mgr.Y * layout.Y * 100) / 100f / 2f;
        CaptureCamera.orthographicSize = ortho;
    }

    void do_regen(List<ProvinceMarker> provs, List<ConnectionMarker> conns, NodeLayout layout) 
    {
        ArtManager.s_art_manager.RegenerateElements(provs, conns, layout);
    }

    public void RegenerateElements(List<ProvinceMarker> provs, List<ConnectionMarker> conns, NodeLayout layout)
    {
        StartCoroutine(perform_async(() => do_regen(provs, conns, layout)));
    }

    IEnumerator perform_async(System.Action function, bool show_log = false)
    {
        LoadingScreen.SetActive(true);

        if (show_log)
        {
            //LogScreen.SetActive(true); 
            //ClearLog();
        }

        yield return null;
        yield return new WaitUntil(() => LoadingScreen.activeInHierarchy);

        if (function != null)
        {
            function();
        }

        LoadingScreen.SetActive(false);
        //LogScreen.SetActive(false);
    }

    public void OnSeasonChanged()
    {
        if (m_season == Season.SUMMER)
        {
            m_season = Season.WINTER;
        }
        else
        {
            m_season = Season.SUMMER;
        }

        GetComponent<AudioSource>().PlayOneShot(ClickAudio);

        StartCoroutine(perform_async(() => do_season_change()));
    }

    public void OnQuitPressed()
    {
        Application.Quit();
    }

    void do_season_change()
    {
        ArtManager.s_art_manager.ChangeSeason(m_season);
    }

    public void ShowOutputWindow()
    {
        GetComponent<AudioSource>().PlayOneShot(ClickAudio);

        if (!OutputWindow.activeSelf)
        {
            OutputWindow.SetActive(true);
        }
    }

    public void GenerateOutput()
    {
        string str = MapName.text;

        if (string.IsNullOrEmpty(str))
        {
            return;
        }

        OutputWindow.SetActive(false);
        GetComponent<AudioSource>().PlayOneShot(ClickAudio);

        StartCoroutine(output_async(str));
    }

    IEnumerator output_async(string str, bool show_log = false)
    {
        LoadingScreen.SetActive(true);

        if (show_log)
        {
            //LogScreen.SetActive(true); 
            //ClearLog();
        }

        yield return null;
        yield return new WaitUntil(() => LoadingScreen.activeInHierarchy);

        ElementManager mgr = GetComponent<ElementManager>();
        NodeLayout layout = WorldGenerator.GetLayout();

        MapFileWriter.GenerateText(str, layout, mgr, m_nations, new Vector2(-mgr.X, -mgr.Y), new Vector2(mgr.X * (layout.X - 1), mgr.Y * (layout.Y - 1)), mgr.Provinces, m_teamplay);

        yield return null;

        MapFileWriter.GenerateImage(str, mgr.Texture); // summer

        mgr.ShowLabels(true);

        yield return null;

        MapFileWriter.GenerateImage(str + "_with_labels", mgr.Texture, false); // labeled image

        mgr.ShowLabels(false);

        if (m_season == Season.SUMMER)
        {
            m_season = Season.WINTER;
        }
        else
        {
            m_season = Season.SUMMER;
        }

        do_season_change();

        yield return new WaitUntil(() => ArtManager.s_art_manager.JustChangedSeason);
        yield return new WaitForEndOfFrame(); // possibly not needed

        ArtManager.s_art_manager.CaptureCam.Render(); // possibly not needed

        yield return new WaitForEndOfFrame(); // possibly not needed

        MapFileWriter.GenerateImage(str + "_winter", mgr.Texture); // winter

        if (m_season == Season.SUMMER)
        {
            m_season = Season.WINTER;
        }
        else
        {
            m_season = Season.SUMMER;
        }

        do_season_change();

        GetComponent<AudioSource>().PlayOneShot(AcceptAudio);

        LoadingScreen.SetActive(false);
        //LogScreen.SetActive(false);
    }

    public void OnCluster()
    {
        m_cluster_water = !m_cluster_water;
    }

    public void OnGeneric()
    {
        m_generic_starts = !m_generic_starts;

        update_nations();
    }

    public void OnTeamplay()
    {
        m_teamplay = !m_teamplay;

        update_nations();
    }

    public void OnHideOptions()
    {
        GameObject o = HideableOptions[0];

        if (o.activeSelf)
        {
            foreach (GameObject obj in HideableOptions)
            {
                obj.SetActive(false);
            }
        }
        else
        {
            foreach (GameObject obj in HideableOptions)
            {
                obj.SetActive(true);
            }
        }

        GetComponent<AudioSource>().PlayOneShot(ClickAudio);
    }

    void hide_controls()
    {
        GameObject o = HideableControls[0];

        if (o.activeSelf)
        {
            foreach (GameObject obj in HideableControls)
            {
                obj.SetActive(false);
            }
        }
        else
        {
            foreach (GameObject obj in HideableControls)
            {
                obj.SetActive(true);
            }
        }
    }

    public void OnPlayerCountChanged(Dropdown d)
    {
        string str = d.captionText.text;
        string trim = str.Replace(" Players", string.Empty);
        int players = 2;
        int.TryParse(trim, out players);

        m_player_count = players;

        update_nations();
    }

    public void OnAgeChanged(Dropdown d)
    {
        string str = d.captionText.text;

        if (str == "Middle Ages")
        {
            m_age = Age.MIDDLE;
        }
        else if (str == "Early Ages")
        {
            m_age = Age.EARLY;
        }
        else if (str == "Late Ages")
        {
            m_age = Age.LATE;
        }
        else
        {
            m_age = Age.ALL;
        }

        foreach (GameObject obj in m_content)
        {
            GameObject.Destroy(obj);
        }

        m_content = new List<GameObject>();

        update_nations();
    }

    void populate_nations(Dropdown d, int i)
    {
        var list = AllNationData.AllNations.Where(x => (x.Age == m_age || m_age == Age.ALL) && x.ID != -1);

        if (m_generic_starts)
        {
            list = AllNationData.AllNations.Where(x => x.ID == -1);
            i = 0;
        }

        d.options.Clear();

        foreach (NationData nd in list)
        {
            d.options.Add(new Dropdown.OptionData(nd.Name));
        }

        d.value = -1; // hard reset the value with this trick
        d.value = i;
    }

    void update_nations()
    {
        List<Dropdown.OptionData> list = new List<Dropdown.OptionData>();

        foreach (NodeLayout layout in m_layouts.Layouts.Where(x => x.NumPlayers == m_player_count))
        {
            Dropdown.OptionData od = new Dropdown.OptionData(layout.Name);
            list.Add(od);
        }

        LayoutDropdown.ClearOptions();
        LayoutDropdown.AddOptions(list);
        LayoutDropdown.value = 0;

        while (m_content.Count > m_player_count)
        {
            GameObject obj = m_content[m_content.Count - 1];
            m_content.RemoveAt(m_content.Count - 1);

            GameObject.Destroy(obj);
        }

        RectTransform tf = ScrollPanel.GetComponent<RectTransform>();
        tf.sizeDelta = new Vector2(247f, 2f + m_player_count * 34f);

        for (int i = 0; i < m_player_count; i++)
        {
            if (m_content.Count > i)
            {
                GameObject obj = m_content[i];
                RectTransform rt = obj.GetComponent<RectTransform>();
                NationPicker np = obj.GetComponent<NationPicker>();
                np.Initialize();
                np.SetTeamplay(m_teamplay);

                populate_nations(np.NationDropdown, i);

                rt.localPosition = new Vector3(0, -17 - (i * 34), 0);
                rt.sizeDelta = new Vector2(0, 34);
                rt.anchoredPosition = new Vector2(0, -17 - (i * 34));
            }
            else
            {
                GameObject cnt = GameObject.Instantiate(NationPicker);
                RectTransform rt = cnt.GetComponent<RectTransform>();
                NationPicker np = cnt.GetComponent<NationPicker>();
                np.Initialize();
                np.SetTeamplay(m_teamplay);

                populate_nations(np.NationDropdown, i);

                rt.SetParent(tf);
                rt.localPosition = new Vector3(0, -17 - (i * 34), 0);
                rt.sizeDelta = new Vector2(0, 34);
                rt.anchoredPosition = new Vector2(0, -17 - (i * 34));

                m_content.Add(cnt);
            }
        }
    }

    void load_layouts()
    {
        m_layouts = new NodeLayoutCollection();

        string data_folder = Application.dataPath;
        string folder = data_folder + "/Layouts/";

        foreach (string file in Directory.GetFiles(folder))
        {
            if (file.Contains(".meta"))
            {
                continue;
            }

            string contents = File.ReadAllText(file);
            var serializer = new XmlSerializer(typeof(NodeLayoutCollection));
            NodeLayoutCollection result;

            using (TextReader reader = new StringReader(contents))
            {
                result = (NodeLayoutCollection) serializer.Deserialize(reader);
            }

            m_layouts.Add(result);
        }
    }
}
