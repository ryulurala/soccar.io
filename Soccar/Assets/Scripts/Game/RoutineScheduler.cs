﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RoutineScheduler : MonoBehaviour
{
    private Coroutine _movePlayersCoroutine;
    private Coroutine _moveBallsCoroutine;

    private Coroutine _scoreNotificationCoroutine;

    private Coroutine _crowdScreamCoroutine;

    [SerializeField]
    private Text _scorer;
    [SerializeField]
    private Text _conceder;
    [SerializeField]
    private GameObject _scoreMark;
    private string[] _colors = { "<color=#008200>", "<color=#000082>", "<color=#820000>", "<color=#828200>", "<color=#820082>", "<color=#828282>" };

    [SerializeField]
    private float _fadeTime;

    public void StartPlayerMoving(Packet.ReceivingPlayerTransform receivingPlayerTransform)
    {
        // 현재 플레이어 위치 저장
        Vector3[] currentPlayerPositions = new Vector3[GameLauncher.Headcount];
        for (int i = 0; i < GameLauncher.Headcount; i++)
        {
            try
            {
                currentPlayerPositions[i] = PlayerController.Players[i].transform.position;
            }
            catch (Exception e) { }
        }

        // 플레이어를 움직임
        _movePlayersCoroutine = StartCoroutine(MovePlayers(currentPlayerPositions, receivingPlayerTransform));
    }

    public void StartBallMoving(Packet.ReceivingBallTransform receivingBallTransform)
    {
        // 현재 공 위치 저장
        Vector3[] currentBallPositions = new Vector3[2];
        Vector3[] currentBallRotations = new Vector3[2];
        for(int i = 0; i < 2; i++)
        {
            currentBallPositions[i] = GameLauncher.Balls[i].transform.position;
            currentBallRotations[i] = GameLauncher.Balls[i].transform.eulerAngles;
        }

        // 공을 움직임
        _moveBallsCoroutine = StartCoroutine(MoveBalls(currentBallPositions, currentBallRotations, receivingBallTransform));
    }

    private IEnumerator MovePlayers(Vector3[] prePositions, Packet.ReceivingPlayerTransform receivingPlayerTransform)
    {
        Vector3[] destPositions = receivingPlayerTransform.PlayerPositions;
        float t = 0;
        AnimFollow.HashIDs_AF hash = PlayerController.Hash;

        for (int k = 0; k < destPositions.Length; k++)
        {
            // 태클 중인 경우 플레이어를 회전시키지 않음
            try
            {
                if (PlayerController.PlayerAnimators[k].GetCurrentAnimatorStateInfo(0).fullPathHash == hash.Tackle
                        || receivingPlayerTransform.AnimHashCodes[k] == hash.TackleTrigger)
                    continue;

                PlayerController.Players[k].transform.eulerAngles = receivingPlayerTransform.PlayerRotations[k];
            }
            catch (MissingReferenceException e) { }
        }

        for (int i = 0; i < 10; i++)
        {
            t += 0.1f;

            for (int j = 0; j < destPositions.Length; j++)
            {
                try
                {
                    if (PlayerController.Players[j].transform.root.GetChild(1).gameObject.GetComponent<AnimFollow.RagdollControl_AF>().falling ||
                        PlayerController.Players[j].transform.root.GetChild(1).gameObject.GetComponent<AnimFollow.RagdollControl_AF>().gettingUp)
                        continue;

                    // 플레이어 이동
                    PlayerController.Players[j].transform.position = new Vector3(
                        Mathf.Lerp(prePositions[j].x, destPositions[j].x, t), 
                        prePositions[j].y,
                        Mathf.Lerp(prePositions[j].z, destPositions[j].z, t)
                    );
                    Vector3 newVector = new Vector3(PlayerController.Players[j].transform.position.x, 0.1f, PlayerController.Players[j].transform.position.z);
                    PlayerController.MiniMapManager.Players[j].transform.localPosition = newVector;
                    PlayerController.PlayerAnimators[j].SetFloat(hash.SpeedFloat, receivingPlayerTransform.PlayerSpeeds[j] / 5);

                    // 애니메이션을 실행시키지 않는 조건
                    if (receivingPlayerTransform.AnimHashCodes[j] == 0 || PlayerController.PlayerAnimators[j].IsInTransition(0))
                        continue;
                    int curAnimHashCode = PlayerController.PlayerAnimators[j].GetCurrentAnimatorStateInfo(0).fullPathHash;
                    if (curAnimHashCode == hash.Tackle || curAnimHashCode == hash.Shoot || curAnimHashCode == hash.Jump)
                        continue;

                    if(receivingPlayerTransform.AnimHashCodes[j] == hash.TackleTrigger)
                        GameLauncher.Sound.SlidingTackle.Play();
                    else if(receivingPlayerTransform.AnimHashCodes[j] == hash.JumpTrigger)
                        GameLauncher.Sound.Jump.Play();
                    // 애니메이션 실행
                    PlayerController.PlayerAnimators[j].SetTrigger(receivingPlayerTransform.AnimHashCodes[j]);
                }
                catch (Exception e) { }
            }

            yield return new WaitForSeconds(0.002f);
        }
    }

    private IEnumerator MoveBalls(Vector3[] prePositions, Vector3[] preRotations, Packet.ReceivingBallTransform receivingBallTransform)
    {
        float t = 0;

        Vector3[] destPositions = receivingBallTransform.BallPositions;
        Vector3[] destRotations = receivingBallTransform.BallRotations;

        for (int i = 0; i < 10; i++)
        {
            t += 0.1f;

            for (int j = 0; j < destPositions.Length; j++)
            {
                if (PlayerController.PlayerIndex != PlayerController.SuperClientIndex)
                {
                    GameLauncher.Balls[j].transform.position = Vector3.Lerp(prePositions[j], destPositions[j], t);
                    GameLauncher.Balls[j].transform.eulerAngles = Vector3.Lerp(preRotations[j], destRotations[j], t);
                }
                Vector3 newVector = new Vector3(GameLauncher.Balls[j].transform.position.x, 0.2f, GameLauncher.Balls[j].transform.position.z);
                PlayerController.MiniMapManager.Balls[j].transform.localPosition = newVector;
            }

            yield return new WaitForSeconds(0.002f);
        }
    }

    public void StopPlayerMoving()
    {
        if (_movePlayersCoroutine != null)
            StopCoroutine(_movePlayersCoroutine);
    }

    public void StopBallMoving()
    {
        if(_moveBallsCoroutine != null)
            StopCoroutine(_moveBallsCoroutine);
    }

    public void NoticeScore(int scorer, int conceder)
    {
        if(_scoreNotificationCoroutine != null)
            StopCoroutine(_scoreNotificationCoroutine);
        _scoreNotificationCoroutine = StartCoroutine(NoticeScoreCoroutine(scorer, conceder));
    }

    private IEnumerator NoticeScoreCoroutine(int scorer, int conceder)
    {
        _scorer.text = _colors[scorer] + PlayerController.PlayerInformations[scorer].PlayerName + "</color>";
        _conceder.text = _colors[conceder] + PlayerController.PlayerInformations[conceder].PlayerName + "</color>";

        _scorer.transform.localScale = new Vector3(1, 1, 1);
        _conceder.transform.localScale = new Vector3(1, 1, 1);
        _scoreMark.transform.localScale = new Vector3(1, 1, 1);

        yield return new WaitForSeconds(4);

        _scorer.transform.localScale = new Vector3(0, 0, 0);
        _conceder.transform.localScale = new Vector3(0, 0, 0);
        _scoreMark.transform.localScale = new Vector3(0, 0, 0);
    }

    public void CrowdScream()
    {
        // 관중 환호
        if(_crowdScreamCoroutine != null)
        {
            StopCoroutine(_crowdScreamCoroutine);
            GameLauncher.Sound.CrowdGoal.volume = 0.5f;
        }
        _crowdScreamCoroutine = StartCoroutine(CrowdScreamCoroutine());
    }

    private IEnumerator CrowdScreamCoroutine()
    {
        float startVolume = GameLauncher.Sound.CrowdGoal.volume;
        GameLauncher.Sound.CrowdGoal.Play();

        // 볼륨 페이드아웃
        while(GameLauncher.Sound.CrowdGoal.volume > 0)
        {
            GameLauncher.Sound.CrowdGoal.volume -= startVolume * Time.fixedDeltaTime / _fadeTime;
            yield return null;
        }

        GameLauncher.Sound.CrowdGoal.Stop();
        GameLauncher.Sound.CrowdGoal.volume = startVolume;
    }
}
