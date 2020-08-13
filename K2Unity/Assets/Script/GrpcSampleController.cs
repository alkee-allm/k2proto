using Grpc.Core;
using System;
using UnityEngine;
using UnityEngine.UI;

public class GrpcSampleController
    : MonoBehaviour
{
    public Text log;

    public InputField id;
    public InputField pw;
    public Button connectButton;

    public GameObject commandContainer;
    public Dropdown command;
    public InputField subCommand;

    ChannelCredentials credentials;
    Channel channel;
    Metadata headers;
    System.Threading.CancellationTokenSource canceler;

    K2.Init.InitClient initClient;
    K2.Push.PushClient pushClient;
    K2.SimpleSample.SimpleSampleClient simpleSampleClient;
    K2.PushSample.PushSampleClient pushSampleClient;


    // Start is called before the first frame update
    void Start()
    {
        credentials = ChannelCredentials.Insecure; // HTTP
        channel = new Channel(Const.SERVER_ADDRESS, credentials);
        headers = new Metadata();
        initClient = new K2.Init.InitClient(channel);
        log.text = "";

        GrpcBegin(initClient.StateAsync(Const.K2NULL),
            (state) =>
            {
                log.text = $"version = {state.Version}\ngateway = {state.Gateway}";
            }
        );
    }

    private void OnApplicationQuit()
    {
        // 더이상 push 이벤트받지 않도록 loop 에서 제거.
        // cancel 하지 않으면 UnityEditor 가 무한루프 대기상태가 되어버리므로 주의
        // https://github.com/alkee-allm/k2proto/issues/23#issuecomment-672790305
        StopPushListening();
    }

    private void OnDisable()
    {
        // play 중 .cs 파일을 수정하는 경우, 무한대기하는 문제를 피하기 위함
        // https://github.com/alkee-allm/k2proto/issues/23#issuecomment-673407337
        StopPushListening();
    }

    public void StopPushListening()
    {
        canceler?.Cancel();
    }

    public void OnConnectButtonClick()
    {
        if (id.text.Length < 1 || pw.text.Length < 1) return;
        connectButton.enabled = false; // 중복 명령 피하기 위해
        log.text = "connecting...";

        if (initClient == null) initClient = new K2.Init.InitClient(channel); // 실행중 script 가 변경되어 reload 되면 null 이 되어버릴 수 있다.
        GrpcBegin(initClient.LoginAsync(new K2.LoginRequest { Id = id.text, Pw = pw.text }), OnLogin,
            (error) =>
            {
                log.text = $"LOGIN ERROR : {error.Message}";
                connectButton.enabled = true;
            }
        );
    }

    public void OnCommandButtonClick()
    {
        log.text = "";
        var option = command.options[command.value];
        switch (option.text)
        {
            case "broadcast":
                GrpcBegin(pushSampleClient.BroadacastAsync(new K2.BroadacastRequest { Message = subCommand.text }, headers: headers));
                return;
            case "hello":
                GrpcBegin(pushSampleClient.HelloAsync(Const.K2NULL, headers: headers));
                return;
            case "message":
                var pos = subCommand.text.IndexOf(' ');
                if (pos < 0)
                {
                    log.text = "message command needs TARGET and MESSAGE parameter that would separated by space";
                    return;
                }
                var target = subCommand.text.Substring(0, pos);
                var message = subCommand.text.Substring(pos + 1);
                GrpcBegin(pushSampleClient.MessageAsync(new K2.MessageRequest { Target = target, Message = message }, headers: headers));
                return;
            case "kick":
                GrpcBegin(pushSampleClient.KickAsync(new K2.KickRequest { Target = subCommand.text }, headers: headers));
                return;
        }
    }

    #region internal helper
    private async void GrpcBegin<RSP>(AsyncUnaryCall<RSP> call, Action<RSP> completed = null, Action<RpcException> error = null)
    {
        try
        {
            var r = await call;
            completed?.Invoke(r);
        }
        catch (RpcException e)
        {
            if (error == null) log.text = e.Message;
            else error.Invoke(e);
        }
    }

    private async void PushEventBegin()
    {
        pushClient = new K2.Push.PushClient(channel);
        var call = pushClient.PushBegin(Const.K2NULL, headers: headers);
        RpcException exception = null;

        using (call)
        {
            try
            {
                while (await call.ResponseStream.MoveNext(canceler.Token))
                {
                    var rsp = call.ResponseStream.Current;
                    log.text = $"({DateTime.Now}) PUSH received : [{rsp.Type}] {rsp.Message} ({rsp.Extra})";
                    if (rsp.Type == K2.PushResponse.Types.PushType.Config && rsp.Message == "jwt")
                    {
                        headers.Clear();
                        headers.Add("Authorization", "Bearer " + rsp.Extra); // update jwt

                        // push 연결되고 새로운 access token 으로, sample 명령을 수행할 수 있다.
                        simpleSampleClient = new K2.SimpleSample.SimpleSampleClient(channel);
                        pushSampleClient = new K2.PushSample.PushSampleClient(channel);
                    }
                }
            }
            catch (RpcException e)
            {
                if (canceler.IsCancellationRequested)
                {
                    Debug.Log("stopped to listen push event by canceling");
                }
                else
                {
                    exception = e;
                }
            }

            // 연결이 끊어짐
            log.text = "Connection closed";
            if (exception != null) log.text += $" {exception.Message}";

            connectButton.transform.parent.gameObject.SetActive(true);
            commandContainer.SetActive(false);
        }
    }
    #endregion

    #region Grpc response handler
    private void OnLogin(K2.LoginResponse rsp)
    {
        connectButton.enabled = true;
        if (rsp.Result != K2.LoginResponse.Types.ResultType.Ok)
        {
            log.text = $"LOGIN ERROR : {rsp.Result}";
            return;
        }

        // 로그인 성공.
        connectButton.transform.parent.gameObject.SetActive(false); // login 관련 UI 숨기기
        commandContainer.SetActive(true); // 명령 UI 보이기

        // update authentication JWT ; https://dotnetcorecentral.com/blog/streaming-and-authentication-in-grpc/
        // channel 을 update 하고싶었지만, InsecureChannel 에서는 불가능 ; https://github.com/alkee-allm/k2proto/issues/23#issuecomment-672693274
        headers.Clear();
        headers.Add("Authorization", "Bearer " + rsp.Jwt);

        log.text = "header updated : " + rsp.Jwt;

        // begin to listen push message
        canceler = new System.Threading.CancellationTokenSource();
        PushEventBegin();
    }


    #endregion
}
