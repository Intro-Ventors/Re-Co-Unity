using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.WebRTC;
using System.Text;
using UnityEngine.UI;

public class Webrtc : MonoBehaviour
{
#pragma warning disable 0649
    [SerializeField] private RawImage receiveImage;
#pragma warning restore 0649

    private RTCPeerConnection mobile, server;
    private List<RTCRtpReceiver> captures;
    private MediaStream captureStream;
    private RTCDataChannel remoteDataChannel;
    private Coroutine sdpCheck;
    private string msg;
    private DelegateOnIceConnectionChange mobileOnIceConnectionChange;
    private DelegateOnIceConnectionChange serverOnIceConnectionChange;
    private DelegateOnIceCandidate mobileOnIceCandidate;
    private DelegateOnIceCandidate serverOnIceCandidate;
    private DelegateOnTrack serverOntrack;
    private DelegateOnNegotiationNeeded mobileOnNegotiationNeeded;
    private DelegateOnNegotiationNeeded serverOnNegotiationNeeded;
    private StringBuilder trackInfos;
    private bool videoUpdateStarted;

    private RTCOfferOptions _offerOptions = new RTCOfferOptions
    {
        iceRestart = false,
        offerToReceiveAudio = true,
        offerToReceiveVideo = true
    };

    private RTCAnswerOptions _answerOptions = new RTCAnswerOptions
    {
        iceRestart = false,
    };

    private void Awake()
    {
        WebRTC.Initialize();
    }

    private void OnDestroy()
    {
        Audio.Stop();
        WebRTC.Dispose();
    }

    // Start is called before the first frame update
    void Start()
    {
        trackInfos = new StringBuilder();
        captures = new List<RTCRtpReceiver>();

        mobileOnIceConnectionChange = state => { OnIceConnectionChange(mobile, state); };
        serverOnIceConnectionChange = state => { OnIceConnectionChange(server, state); };
        mobileOnIceCandidate = candidate => { OnIceCandidate(mobile, candidate); };
        serverOnIceCandidate = candidate => { OnIceCandidate(server, candidate); };
        serverOntrack = e => { captureStream.AddTrack(e.Track); };
        mobileOnNegotiationNeeded = () => { StartCoroutine(PcOnNegotiationNeeded(mobile)); };
        serverOnNegotiationNeeded = () => { StartCoroutine(PcOnNegotiationNeeded(server)); };
    }

    private static RTCConfiguration GetSelectedSdpSemantics()
    {
        RTCConfiguration config = default;
        config.iceServers = new[]
        {
            new RTCIceServer { urls = new[] { "stun:stun.l.google.com:19302" } }
        };

        return config;
    }

    private void OnIceConnectionChange(RTCPeerConnection pc, RTCIceConnectionState state)
    {
        switch (state)
        {
            case RTCIceConnectionState.New:
                Debug.Log($"{GetName(pc)} IceConnectionState: New");
                break;
            case RTCIceConnectionState.Checking:
                Debug.Log($"{GetName(pc)} IceConnectionState: Checking");
                break;
            case RTCIceConnectionState.Closed:
                Debug.Log($"{GetName(pc)} IceConnectionState: Closed");
                break;
            case RTCIceConnectionState.Completed:
                Debug.Log($"{GetName(pc)} IceConnectionState: Completed");
                break;
            case RTCIceConnectionState.Connected:
                Debug.Log($"{GetName(pc)} IceConnectionState: Connected");
                break;
            case RTCIceConnectionState.Disconnected:
                Debug.Log($"{GetName(pc)} IceConnectionState: Disconnected");
                break;
            case RTCIceConnectionState.Failed:
                Debug.Log($"{GetName(pc)} IceConnectionState: Failed");
                break;
            case RTCIceConnectionState.Max:
                Debug.Log($"{GetName(pc)} IceConnectionState: Max");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, null);
        }
    }

    IEnumerator PcOnNegotiationNeeded(RTCPeerConnection pc)
    {
        Debug.Log($"{GetName(pc)} createOffer start");
        var op = pc.CreateOffer(ref _offerOptions);
        yield return op;

        if (!op.IsError)
        {
            yield return StartCoroutine(OnCreateOfferSuccess(pc, op.Desc));
        }
        else
        {
            OnCreateSessionDescriptionError(op.Error);
        }
    }

    //private void AddTracks()
    //{
    //    foreach (var track in captureStream.GetTracks())
    //    {
    //        captures.Add(mobile.AddTrack(track, captureStream));
    //    }
    //
    //    if (!videoUpdateStarted)
    //    {
    //        StartCoroutine(WebRTC.Update());
    //        videoUpdateStarted = true;
    //    }
    //}
    //
    //private void RemoveTracks()
    //{
    //    foreach (var sender in captures)
    //    {
    //        mobile.RemoveTrack(sender);
    //    }
    //
    //    captures.Clear();
    //    trackInfos.Clear();
    //}

    private void Call()
    {
        Debug.Log("GetSelectedSdpSemantics");
        var configuration = GetSelectedSdpSemantics();
        mobile = new RTCPeerConnection(ref configuration);
        Debug.Log("Created local peer connection object mobile");
        mobile.OnIceCandidate = mobileOnIceCandidate;
        mobile.OnIceConnectionChange = mobileOnIceConnectionChange;
        mobile.OnNegotiationNeeded = mobileOnNegotiationNeeded;
        server = new RTCPeerConnection(ref configuration);
        Debug.Log("Created remote peer connection object server");
        server.OnIceCandidate = serverOnIceCandidate;
        server.OnIceConnectionChange = serverOnIceConnectionChange;
        server.OnNegotiationNeeded = serverOnNegotiationNeeded;
        server.OnTrack = serverOntrack;

        mobile.CreateDataChannel("data");

        captureStream = new MediaStream();
        captureStream.OnAddTrack = e =>
        {
            if (e.Track is VideoStreamTrack track)
            {
                receiveImage.texture = track.InitializeReceiver(1280, 720);
            }
        };
    }

    private void HangUp()
    {
        captureStream.Dispose(); 
        captureStream = null;

        mobile.Close();
        server.Close();
        Debug.Log("Close local/remote peer connection");
        mobile.Dispose();
        server.Dispose();
        mobile= null;
        server = null;
        receiveImage.texture = null;
    }

    private void OnIceCandidate(RTCPeerConnection pc, RTCIceCandidate candidate)
    {
        GetOtherPc(pc).AddIceCandidate(candidate);
        Debug.Log($"{GetName(pc)} ICE candidate:\n {candidate.Candidate}");
    }

    private void OnTrack(RTCPeerConnection pc, RTCTrackEvent e)
    {
        //serverSenders.Add(pc.AddTrack(e.Track, captureStream));
        trackInfos.Append($"{GetName(pc)} receives remote track:\r\n");
        trackInfos.Append($"Track kind: {e.Track.Kind}\r\n");
        trackInfos.Append($"Track id: {e.Track.Id}\r\n");
    }

    private string GetName(RTCPeerConnection pc)
    {
        return (pc == mobile) ? "mobile" : "server";
    }

    private RTCPeerConnection GetOtherPc(RTCPeerConnection pc)
    {
        return (pc == mobile) ? server : mobile;
    }

    private IEnumerator OnCreateOfferSuccess(RTCPeerConnection pc, RTCSessionDescription desc)
    {
        Debug.Log($"Offer from {GetName(pc)}\n{desc.sdp}");
        Debug.Log($"{GetName(pc)} setLocalDescription start");
        var op = pc.SetLocalDescription(ref desc);
        yield return op;

        if (!op.IsError)
        {
            OnSetLocalSuccess(pc);
        }
        else
        {
            var error = op.Error;
            OnSetSessionDescriptionError(ref error);
        }

        var otherPc = GetOtherPc(pc);
        Debug.Log($"{GetName(otherPc)} setRemoteDescription start");
        var op2 = otherPc.SetRemoteDescription(ref desc);
        yield return op2;
        if (!op2.IsError)
        {
            OnSetRemoteSuccess(otherPc);
        }
        else
        {
            var error = op2.Error;
            OnSetSessionDescriptionError(ref error);
        }
        Debug.Log($"{GetName(otherPc)} createAnswer start");
        // Since the 'remote' side has no media stream we need
        // to pass in the right constraints in order for it to
        // accept the incoming offer of audio and video.

        var op3 = otherPc.CreateAnswer(ref _answerOptions);
        yield return op3;
        if (!op3.IsError)
        {
            yield return OnCreateAnswerSuccess(otherPc, op3.Desc);
        }
        else
        {
            OnCreateSessionDescriptionError(op3.Error);
        }
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        Audio.Update(data, data.Length);
    }

    private void OnSetLocalSuccess(RTCPeerConnection pc)
    {
        Debug.Log($"{GetName(pc)} SetLocalDescription complete");
    }

    static void OnSetSessionDescriptionError(ref RTCError error)
    {
        Debug.LogError($"Error Detail Type: {error.message}");
    }

    private void OnSetRemoteSuccess(RTCPeerConnection pc)
    {
        Debug.Log($"{GetName(pc)} SetRemoteDescription complete");
    }

    IEnumerator OnCreateAnswerSuccess(RTCPeerConnection pc, RTCSessionDescription desc)
    {
        Debug.Log($"Answer from {GetName(pc)}:\n{desc.sdp}");
        Debug.Log($"{GetName(pc)} setLocalDescription start");
        var op = pc.SetLocalDescription(ref desc);
        yield return op;

        if (!op.IsError)
        {
            OnSetLocalSuccess(pc);
        }
        else
        {
            var error = op.Error;
            OnSetSessionDescriptionError(ref error);
        }

        var otherPc = GetOtherPc(pc);
        Debug.Log($"{GetName(otherPc)} setRemoteDescription start");

        var op2 = otherPc.SetRemoteDescription(ref desc);
        yield return op2;
        if (!op2.IsError)
        {
            OnSetRemoteSuccess(otherPc);
        }
        else
        {
            var error = op2.Error;
            OnSetSessionDescriptionError(ref error);
        }
    }

    private static void OnCreateSessionDescriptionError(RTCError error)
    {
        Debug.LogError($"Error Detail Type: {error.message}");
    }
}
