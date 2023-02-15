using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
[RequireComponent(typeof(BoxCollider2D))]
public class CharacterController2D : MonoBehaviour
{
    BoxCollider2D coll;
    Vector2 slopeNormalPerp = Vector2.right;
    Vector2 lastSlopeNormalPerp = Vector2.right;
    private bool verticalCollided;
    private bool horizontalCollided;
    private bool verticalSpeedReset;
    private bool horizontalSpeedReset;
    private bool isGround;
    private bool isGrounded;
    private bool edge;

    [HideInInspector]
    public Vector2 velocity;

    [Header("��ײ������")]

    //����֮��ľ���
    [SerializeField]
    [Range(0, 1)]
    float horizontalRayDistance = 0.6f;

    [SerializeField]//���µ����ʱ�����ߵľ���
    [Range(0, 1)]
    float downRayDistance = 0.8f;

    [SerializeField]//�����ƶ�ʱ������Ծ�����ϼ�������ֱ�ӵľ���
    [Range(0, 1)]
    float upRayDistance = 0f;

    [SerializeField]//�ڵ���ʱ���¼������߾���
    [Range(0, 1)]
    float groundRayDistance = 0.2f;

    [SerializeField]
    [Range(0, 1f)]//��ǽ���е���������ٶ�
    float CollisionHardness = 0.5f;

    [SerializeField]
    [Range(0, 1f)]//�ڵ���ʱ�������ٶȣ�̫С��ɫ�Ե�����ж����ܻ᲻̫�飬̫��Ҳ����ë��
    float gripDegree = 0.08f;

    //ˮƽ��ײ����������Ƕ����µ�б�£��ոպ���ȿ��ܲ��Ṥ���ĺ����������Ե��ı�ʵ����Ҫ��ֵ��һЩ
    //*ע��*���������ֻ����ˮƽ��ײ�����߼������б�£��������ƽ�ɫ���£�����и�������Ƕȵ�б�³��ֲ�����������
    [SerializeField]
    [Range(0, 90)]
    float horizontalIgnoreDegree = 46f;

    float verticalCheckDistance;
    float horizontalCheckDistance;

    float sizeX;
    float sizeY;

    public LayerMask platformMask;

    public bool drawDebugRay = false;

    void Awake()
    {
        coll = gameObject.GetComponent<BoxCollider2D>();
    }

    private void FixedUpdate()
    {
        Move(velocity);
    }

    public void Move(Vector2 movement)
    {
        sizeX = math.abs(transform.localScale.x * coll.size.x);
        sizeY = math.abs(transform.localScale.y * coll.size.y);

        var checkPos = transform.position + new Vector3(coll.offset.x, coll.offset.y, 0);
        var deltaMovement = movement * Time.fixedDeltaTime;

        verticalCheckDistance = math.abs(deltaMovement.y) + sizeY / 2 + 0.5f;
        horizontalCheckDistance = math.abs(deltaMovement.x) + sizeX / 2 + 0.5f;

        //�ж��Ƿ���ƽ̨�ı�Ե
        edge = !Physics2D.Raycast(checkPos + Vector3.up * sizeY / 2, Vector2.down, verticalCheckDistance + sizeY / 2, platformMask);

        //����Ƿ������б����
        RaycastHit2D[] Hit = new RaycastHit2D[2];
        RayVertical(checkPos, Vector2.down, ref Hit[0], ref Hit[1], downRayDistance);

        //�����⵽��б����ô�����ڱ�Ե
        if (math.abs((Hit[0].distance < Hit[1].distance ? Hit[0].normal : Hit[1].normal).x) > 0.001f)
            edge = false;

        ResetState();

        if (isGrounded & movement.y <= 0)//�ڵ����ƶ�ʱ������
        {
            MoveGround(ref deltaMovement, checkPos);
            transform.Translate(deltaMovement);

            MoveEnd();
            return;
        }

        if (deltaMovement.x != 0 || deltaMovement.y != 0)
        {
            if (deltaMovement.y >= 0)
            {
                MoveVertical(ref deltaMovement, checkPos);
                checkPos.y += deltaMovement.y;
                MoveHorizontal(ref deltaMovement, checkPos);
                transform.Translate(deltaMovement);
            }
            else
            {
                MoveHorizontal(ref deltaMovement, checkPos);
                checkPos.x += deltaMovement.x;
                MoveVertical(ref deltaMovement, checkPos);
                transform.Translate(deltaMovement);
            }
        }

        MoveEnd();

        void ResetState()
        {
            isGrounded = isGround;
            isGround = verticalCollided = horizontalCollided = false;
            verticalSpeedReset = horizontalSpeedReset = true;
            lastSlopeNormalPerp = slopeNormalPerp;
            slopeNormalPerp = Vector2.right;
        }

        //�˶��������ٶȵļ���
        void MoveEnd()
        {
            if (!verticalCollided)
                velocity.y = (isGround ? 0 : deltaMovement.y) / Time.fixedDeltaTime;
            if (!horizontalCollided)
                velocity.x = (isGround ? deltaMovement.x / slopeNormalPerp.x : deltaMovement.x * lastSlopeNormalPerp.x) / Time.fixedDeltaTime;

            //SpeedReset�������ڷ�ֹ���ƶ����򵯳���ʱ������뿪��ײ��ʱ���ٶȹ���
            //ȷ������ײ������ʱ�򲻻�ɵ������ٶȼ���Ϊ�ƶ��ٶ�
            if (horizontalCollided && horizontalSpeedReset) 
                velocity.x = 0;
            if ((verticalCollided || isGround) && verticalSpeedReset) 
                velocity.y = 0;
        }
    }

    //����ײ���ж���ʵ�ֿ���������ʱ����������ٶȣ������ײĿ��ľ������ײ���������߼��ķ���
    //direction�����˶�����/���߼�ⷽ��x��y��ͬ��������ֵ�����ڱ����˶���0���޷��������е���
    float CollisionCheck(float distance, float direction)
    {
        if (distance < 0 && distance < -CollisionHardness)
            return -Math.Sign(direction) * CollisionHardness;
        else
            return distance * Math.Sign(direction);
    }

    //ˮƽ�˶�����ײ�ж�����Ҫ����������ߣ��ƶ����룬���߼�ⷽ��
    void HorizontalCollisionCheck(RaycastHit2D[] Hit, ref Vector2 deltaMovement, float rayDirection)
    {
        rayDirection = math.sign(rayDirection);

        if (Hit[0].distance == 1000f && Hit[1].distance == 1000f && Hit[2].distance == 1000f)
            return;

        if (Hit[0].distance <= Hit[1].distance && Hit[0].distance <= Hit[2].distance)
            Hit[2] = Hit[0];
        else if (Hit[1].distance <= Hit[0].distance && Hit[1].distance <= Hit[2].distance)
            Hit[2] = Hit[1];

        if (Hit[2].distance < deltaMovement.x * rayDirection)
        {
            horizontalCollided = Hit[2].normal != Vector2.zero;
            deltaMovement.x = CollisionCheck(Hit[2].distance, rayDirection);
        }
    }

    //��ֱ�˶�����ײ�ж�����Ҫ����������ߣ��ƶ����룬���߼�ⷽ��
    void VerticalCollisionCheck(RaycastHit2D[] Hit, ref Vector2 deltaMovement, float rayDirection)
    {
        rayDirection = math.sign(rayDirection);

        if (Hit[0].distance == 1000f && Hit[1].distance == 1000f)
            return;

        if (Hit[0].distance <= Hit[1].distance)
            Hit[1] = Hit[0];

        //ʹ��ײ��ƽ̨��Ե��ʱ����ֱ��ˮƽ�ĵ�������ͬʱ�������ᵼ��б�´�ģ���Բ�ʹ���ˣ�
        //if (-Hit[1].distance >= (1 - horizontalRayDistance) * sizeY / 2)
        //    return;

        if (Hit[1].distance < 0 && deltaMovement.y > 0
            && deltaMovement.x * Vector2.SignedAngle(-Vector2.Perpendicular(Hit[1].normal) * deltaMovement.x, Vector2.right * deltaMovement.x)
            < deltaMovement.x * Vector2.SignedAngle(deltaMovement, Vector2.right * deltaMovement.x)) 
            isGround = true;

        if (Hit[1].distance < deltaMovement.y * rayDirection) 
        {
            verticalCollided = true;
            if (rayDirection < 0)
                isGround = true;

            deltaMovement.y = CollisionCheck(Hit[1].distance, rayDirection);
        }
    }

    void MoveGround(ref Vector2 deltaMovement, Vector2 checkPos)
    {
        RaycastHit2D[] Hit = new RaycastHit2D[3];
        var offset = 0f;

        RayVertical(checkPos, Vector2.down, ref Hit[1], ref Hit[2], edge ? downRayDistance : groundRayDistance);

        if (Hit[1].distance < Hit[2].distance)
            Hit[2] = Hit[1];

        slopeNormalPerp = edge ? Vector2.right : -Vector2.Perpendicular(Hit[2].normal).normalized;
        deltaMovement.y = edge ? deltaMovement.y : -math.abs(deltaMovement.x * slopeNormalPerp.y) - gripDegree;

        if (drawDebugRay)
        {
            Debug.DrawRay(Hit[2].point, Hit[2].normal, UnityEngine.Color.red);
            Debug.DrawRay(Hit[2].point, slopeNormalPerp, UnityEngine.Color.red);
        }

        if (deltaMovement.y < -Hit[2].distance)
            isGround = true;

        offset = GroundCollisinCheck(Hit[2].distance, deltaMovement);

        //�뿪��ԵΪб�µ�ƽ̨�����һ�μ�⣬��ʱ��ɫ�Ѿ��뿪�������Լ�ⲻ��б��б�ʣ�����ʹ����һ�μ���б��
        if (slopeNormalPerp == Vector2.zero)
            slopeNormalPerp = lastSlopeNormalPerp;

        deltaMovement.x *= slopeNormalPerp.x;

        //������ײ
        RayHorizontal(checkPos, Vector2.right * deltaMovement.x, ref Hit, horizontalRayDistance);
        HorizontalCollisionCheck(Hit, ref deltaMovement, deltaMovement.x);

        deltaMovement.y = deltaMovement.x / slopeNormalPerp.x * slopeNormalPerp.y + offset;

        //��������ƶ�ʱy�����ײ
        float GroundCollisinCheck(float distance, Vector2 deltaMovement)
        {
            if(distance >= Math.Abs(deltaMovement.y))
                return 0;

            if (distance < 0 && distance < -CollisionHardness)
                return -Math.Sign(deltaMovement.y) * CollisionHardness;
            else
                return distance * Math.Sign(deltaMovement.y);
        }
    }

    void MoveVertical(ref Vector2 deltaMovement, Vector2 checkPos)
    {
        RaycastHit2D[] Hit = new RaycastHit2D[2];
        var rayDistance = (deltaMovement.y < 0) ? (edge ? downRayDistance : groundRayDistance) : upRayDistance;
        var rayDirection = deltaMovement.y != 0 ? Vector2.up * deltaMovement.y : Vector2.up;
        var moveDirection = deltaMovement.y;

        RayVertical(checkPos, rayDirection, ref Hit[0], ref Hit[1], rayDistance);
        VerticalCollisionCheck(Hit, ref deltaMovement, rayDirection.y);
        verticalSpeedReset = verticalCollided;

        if (!isGround && moveDirection >= 0)
        {
            rayDistance = (rayDistance == upRayDistance) ? (edge ? downRayDistance : groundRayDistance) : upRayDistance;
            rayDirection *= -1;
            RayVertical(checkPos, rayDirection, ref Hit[0], ref Hit[1], rayDistance);
            VerticalCollisionCheck(Hit, ref deltaMovement, rayDirection.y);
            verticalSpeedReset = verticalSpeedReset == verticalCollided;
        }
    }

    void MoveHorizontal(ref Vector2 deltaMovement, Vector2 checkPos)
    {
        RaycastHit2D[] Hit = new RaycastHit2D[3];
        var rayDirection = deltaMovement.x != 0 ? Vector2.right * deltaMovement.x : Vector2.right;

        RayHorizontal(checkPos, rayDirection, ref Hit, horizontalRayDistance);
        HorizontalCollisionCheck(Hit, ref deltaMovement, rayDirection.x);
        horizontalSpeedReset = horizontalCollided;

        rayDirection *= -1;
        RayHorizontal(checkPos, rayDirection, ref Hit, horizontalRayDistance);
        HorizontalCollisionCheck(Hit, ref deltaMovement, rayDirection.x);
        horizontalSpeedReset = horizontalSpeedReset==horizontalCollided;
    }

    //���߼�⣬���ҽ�δ���е����ߵľ������ó�1000�����ں����ж�
    void RaySetReturn(ref RaycastHit2D Hit, Vector2 checkPos, Vector2 direction, float size, float CheckDistance)
    {
        if(drawDebugRay)
            Debug.DrawRay(checkPos, direction.normalized * CheckDistance);

        Hit = Physics2D.Raycast(checkPos, direction, CheckDistance, platformMask);
        Hit.distance -= size / 2;
        if (!Hit)
            Hit.distance = 1000f;
    }

    //��ֱ����������������߼��
    void RayVertical(Vector2 checkPos, Vector2 direction, ref RaycastHit2D rayHit1, ref RaycastHit2D rayHit2, float rayDistance)
    {
        checkPos.x += rayDistance * sizeX / 2;
        RaySetReturn(ref rayHit1, checkPos, direction, sizeY, verticalCheckDistance);

        checkPos.x -= rayDistance * sizeX;
        RaySetReturn(ref rayHit2, checkPos, direction, sizeY, verticalCheckDistance);
    }

    //ˮƽ����������������߼��
    void RayHorizontal(Vector2 checkPos, Vector2 direction, ref RaycastHit2D[] Hit, float rayDistance)
    {
        RaySetReturn(ref Hit[0], checkPos, direction, sizeX, horizontalCheckDistance);
        sloplimit(ref Hit[0]);

        checkPos.y += rayDistance * sizeY / 2;
        RaySetReturn(ref Hit[1], checkPos, direction, sizeX, horizontalCheckDistance);
        sloplimit(ref Hit[1]);

        checkPos.y -= rayDistance * sizeY;
        RaySetReturn(ref Hit[2], checkPos, direction, sizeX, horizontalCheckDistance);
        sloplimit(ref Hit[2]);

        void sloplimit(ref RaycastHit2D hit)
        {
            if (math.abs(Vector2.Angle(hit.normal, Vector2.up)) > horizontalIgnoreDegree || hit.distance == 1000f)
                return;

            hit.distance += sizeY * (1 - groundRayDistance) / 2;
            hit.normal = Vector2.zero;
        }
    }

    public void LeaveGround()
    {
        isGround = false;
        isGrounded = false;
    }

    public bool GetIsGround()
    {
        return isGround;
    }

    public Vector2 GetSlopeNormalPerp()
    {
        return slopeNormalPerp;
    }

}
