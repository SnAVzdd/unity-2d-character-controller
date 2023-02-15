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

    [Header("碰撞检测参数")]

    //射线之间的距离
    [SerializeField]
    [Range(0, 1)]
    float horizontalRayDistance = 0.6f;

    [SerializeField]//向下掉落的时候射线的距离
    [Range(0, 1)]
    float downRayDistance = 0.8f;

    [SerializeField]//向上移动时（如跳跃）向上检测的射线直接的距离
    [Range(0, 1)]
    float upRayDistance = 0f;

    [SerializeField]//在地面时向下检测的射线距离
    [Range(0, 1)]
    float groundRayDistance = 0.2f;

    [SerializeField]
    [Range(0, 1f)]//从墙体中弹出的最大速度
    float CollisionHardness = 0.5f;

    [SerializeField]
    [Range(0, 1f)]//在地面时的向下速度，太小角色对地面的判定可能会不太灵，太大也会有毛病
    float gripDegree = 0.08f;

    //水平碰撞会无视这个角度以下的斜坡，刚刚好相等可能不会工作的很正常，可以调的比实际需要的值大一些
    //*注意*：这个参数只能让水平碰撞的射线检测无视斜坡，不能限制角色爬坡，如果有高于这个角度的斜坡出现不会正常工作
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

        //判断是否在平台的边缘
        edge = !Physics2D.Raycast(checkPos + Vector3.up * sizeY / 2, Vector2.down, verticalCheckDistance + sizeY / 2, platformMask);

        //检测是否会落在斜坡上
        RaycastHit2D[] Hit = new RaycastHit2D[2];
        RayVertical(checkPos, Vector2.down, ref Hit[0], ref Hit[1], downRayDistance);

        //如果检测到了斜坡那么不算在边缘
        if (math.abs((Hit[0].distance < Hit[1].distance ? Hit[0].normal : Hit[1].normal).x) > 0.001f)
            edge = false;

        ResetState();

        if (isGrounded & movement.y <= 0)//在地面移动时的特例
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

        //运动结束后速度的计算
        void MoveEnd()
        {
            if (!verticalCollided)
                velocity.y = (isGround ? 0 : deltaMovement.y) / Time.fixedDeltaTime;
            if (!horizontalCollided)
                velocity.x = (isGround ? deltaMovement.x / slopeNormalPerp.x : deltaMovement.x * lastSlopeNormalPerp.x) / Time.fixedDeltaTime;

            //SpeedReset变量用于防止向移动方向弹出的时候会在离开碰撞体时把速度归零
            //确保了碰撞弹出的时候不会吧弹出的速度计算为移动速度
            if (horizontalCollided && horizontalSpeedReset) 
                velocity.x = 0;
            if ((verticalCollided || isGround) && verticalSpeedReset) 
                velocity.y = 0;
        }
    }

    //对碰撞的判定，实现卡在物体中时弹出有最大速度，离可碰撞目标的距离和碰撞方向上射线检测的方向
    //direction是与运动方向/射线检测方向（x或y）同正负的数值，用于避免运动是0而无法从物体中弹出
    float CollisionCheck(float distance, float direction)
    {
        if (distance < 0 && distance < -CollisionHardness)
            return -Math.Sign(direction) * CollisionHardness;
        else
            return distance * Math.Sign(direction);
    }

    //水平运动的碰撞判定，需要传入检测的射线，移动距离，射线检测方向
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

    //竖直运动的碰撞判定，需要传入检测的射线，移动距离，射线检测方向
    void VerticalCollisionCheck(RaycastHit2D[] Hit, ref Vector2 deltaMovement, float rayDirection)
    {
        rayDirection = math.sign(rayDirection);

        if (Hit[0].distance == 1000f && Hit[1].distance == 1000f)
            return;

        if (Hit[0].distance <= Hit[1].distance)
            Hit[1] = Hit[0];

        //使得撞上平台边缘的时候竖直和水平的弹出不会同时触发（会导致斜坡穿模所以不使用了）
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

        //离开边缘为斜坡的平台的最后一次检测，此时角色已经离开地面所以检测不到斜坡斜率，所以使用上一次检测的斜率
        if (slopeNormalPerp == Vector2.zero)
            slopeNormalPerp = lastSlopeNormalPerp;

        deltaMovement.x *= slopeNormalPerp.x;

        //横向碰撞
        RayHorizontal(checkPos, Vector2.right * deltaMovement.x, ref Hit, horizontalRayDistance);
        HorizontalCollisionCheck(Hit, ref deltaMovement, deltaMovement.x);

        deltaMovement.y = deltaMovement.x / slopeNormalPerp.x * slopeNormalPerp.y + offset;

        //计算地面移动时y轴的碰撞
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

    //射线检测，并且将未命中的射线的距离设置成1000，便于后续判断
    void RaySetReturn(ref RaycastHit2D Hit, Vector2 checkPos, Vector2 direction, float size, float CheckDistance)
    {
        if(drawDebugRay)
            Debug.DrawRay(checkPos, direction.normalized * CheckDistance);

        Hit = Physics2D.Raycast(checkPos, direction, CheckDistance, platformMask);
        Hit.distance -= size / 2;
        if (!Hit)
            Hit.distance = 1000f;
    }

    //竖直的射线起点计算和射线检测
    void RayVertical(Vector2 checkPos, Vector2 direction, ref RaycastHit2D rayHit1, ref RaycastHit2D rayHit2, float rayDistance)
    {
        checkPos.x += rayDistance * sizeX / 2;
        RaySetReturn(ref rayHit1, checkPos, direction, sizeY, verticalCheckDistance);

        checkPos.x -= rayDistance * sizeX;
        RaySetReturn(ref rayHit2, checkPos, direction, sizeY, verticalCheckDistance);
    }

    //水平的射线起点计算和射线检测
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
