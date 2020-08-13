using UnityEngine;
using UnityEditor;

public class GrpcBuilder : EditorWindow
{

    [MenuItem("Window/K2/Grpc")]
    static void Init()
    {
        var window = GetWindow(typeof(GrpcBuilder)) as GrpcBuilder;
        window.Show();
    }

    private void OnGUI()
    {
        GUIStyle headerLabel = new GUIStyle();
        headerLabel.alignment = TextAnchor.MiddleCenter;
        headerLabel.fontStyle = FontStyle.Bold;

        GUILayout.Label("GRPC builder", headerLabel);
        EditorGUILayout.HelpBox("not supported yet", MessageType.Info, true);

        // TOOD: protobuf message & gRPC build 자동화(Tools/GenerateProto.bat)
    }

}
