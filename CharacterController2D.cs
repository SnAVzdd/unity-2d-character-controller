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
    Rigidbody2D Rig;
    Vector2 slopeNormalPerp;
    Vector2 lastSlopeNormalPerp;
    private bool verticalCollided;
    private bool horizontalCollided;
    private bool verticalSpeedReset;
    private bool horizontalSpeedReset;
    private bool below;
    private bool isGround;
    private bool edge;
    [HideInInspector]
    public Vector2 velocity;
    [Header("碰撞检测参数")]
    [SerializeField]//射线检测的距离
    [Range(1, 2)]
    float verticalCheckDistance = 1.2f;

    [SerializeField]
    [Range(1, 2)]
    float horizontalCheckDistance = 1.2f;

    //射线之间的距离
    [SerializeField]//向下掉落的时候射线的距离，即角色在向下掉落的时候的碰撞箱宽度
    [Range(0, 1)]
    float verticalRayDistance = 0.8f;

    [SerializeField]
    [Range(0, 1)]
    float horizontalRayDistance = 0.5f;

    [SerializeField]//特殊情况下的向下射线距离，调小可以使得角色爬坡时贴合斜坡，跳跃不容易被上方物体卡住，只要不是下落都会使用这个
    [Range(0, 1)]
    float specialRayDistance = 0.2f;

    [SerializeField]
    [Range(0, 1f)]//从墙体中弹出的最大速度
    float CollisionHardness = 0.1f;

    [SerializeField]
    [Range(0, 1f)]//在地面时的向下速度，太小角色对地面的判定可能会不太灵，太大也会有毛病
    float gripDegree = 0.08f;

    public LayerMask platformMask;


    void Start()
    {
        coll = gameObject.GetComponent<BoxCollider2D>();
    }


    private void FixedUpdate()
    {
        Move(velocity);
    }

    public void Move(Vector2 movement)
    {
        var checkPos = transform.position + new Vector3(coll.offset.x, coll.offset.y, 0);
        var deltaMovement = movement * Time.fixedDeltaTime;
        //判断是否在平台的边缘
        edge = !Physics2D.Raycast(checkPos, Vector2.down, verticalCheckDistance * coll.size.y - coll.size.y / 2, platformMask);
        //检测是否会落在斜坡上
        RaycastHit2D[] Hit = new RaycastHit2D[2];
        RayVertical(checkPos, Vector2.down, ref Hit[0], ref Hit[1], verticalRayDistance);
        //如果检测到了斜坡那么不算在边缘
        if (math.abs((Hit[0].distance < Hit[1].distance ? Hit[0].normal : Hit[1].normal).x) > 0.001f)
            edge = false;

        ResetState();

        if (isGround & movement.y <= 0)//在地面移动时的特例
        {
            MoveGround(ref deltaMovement, checkPos);
            transform.Translate(deltaMovement);

            MoveEnd();
            return;
        }

        if (deltaMovement.y != 0)
            MoveVertical(ref deltaMovement, checkPos);
        if (deltaMovement.x != 0 || deltaMovement.y != 0)
            MoveHorizontal(ref deltaMovement, checkPos);
        transform.Translate(deltaMovement);

        MoveEnd();

        void ResetState()
        {
            isGround = below;
            below = verticalCollided = horizontalCollided = false;
            verticalSpeedReset = horizontalSpeedReset = true;
            lastSlopeNormalPerp = slopeNormalPerp;
        }

        //运动结束后速度的计算
        void MoveEnd()
        {
            //SpeedReset变量用于防止向移动方向弹出的时候会在离开碰撞体时把速度归零
            if (!horizontalCollided)
                velocity.x = deltaMovement.x / Time.fixedDeltaTime;
            if (!verticalCollided)
                velocity.y = deltaMovement.y / Time.fixedDeltaTime;
            //确保了碰撞弹出的时候不会吧弹出的速度计算为移动速度
            if (horizontalCollided && horizontalSpeedReset) 
                velocity.x = 0;
            if ((verticalCollided || below) && verticalSpeedReset) 
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
        else if (Hit[0].distance <= Hit[1].distance && Hit[0].distance <= Hit[2].distance && Hit[0].distance < deltaMovement.x * rayDirection)
        {
            horizontalCollided = true;
            deltaMovement.x = CollisionCheck(Hit[0].distance, rayDirection);
        }
        else if (Hit[1].distance <= Hit[0].distance && Hit[1].distance <= Hit[2].distance && Hit[1].distance < deltaMovement.x * rayDirection)
        {
            horizontalCollided = true;
            deltaMovement.x = CollisionCheck(Hit[1].distance, rayDirection);
        }
        else if (Hit[2].distance <= Hit[0].distance && Hit[2].distance <= Hit[1].distance && Hit[2].distance < deltaMovement.x * rayDirection)
        {
            horizontalCollided = true;
            deltaMovement.x = CollisionCheck(Hit[2].distance, rayDirection);
        }
    }

    //竖直运动的碰撞判定，需要传入检测的射线，移动距离，射线检测方向
    void VerticalCollisionCheck(RaycastHit2D[] Hit, ref Vector2 deltaMovement, float rayDirection)
    {
        rayDirection = math.sign(rayDirection);

        if (Hit[0].distance == 1000f && Hit[1].distance == 1000f)
            return;
        else if (Hit[0].distance <= Hit[1].distance && Hit[0].distance < deltaMovement.y * rayDirection)
        {
            verticalCollided = true;
            if (rayDirection < 0)
                below = true;

            deltaMovement.y = CollisionCheck(Hit[0].distance, rayDirection);
        }
        else if (Hit[0].distance > Hit[1].distance && Hit[1].distance < deltaMovement.y * rayDirection)
        {
            verticalCollided = true;
            if (rayDirection < 0)
                below = true;

            deltaMovement.y = CollisionCheck(Hit[1].distance, rayDirection);
        }
    }

    void MoveGround(ref Vector2 deltaMovement, Vector2 checkPos)
    {
        RaycastHit2D[] Hit = new RaycastHit2D[3];
        var offset = 0f;

        RayVertical(checkPos, Vector2.down, ref Hit[1], ref Hit[2], edge ? verticalRayDistance : specialRayDistance);

        if (Hit[1].distance < Hit[2].distance)
        {
            slopeNormalPerp = edge ? Vector2.right : -Vector2.Perpendicular(Hit[1].normal).normalized;
            deltaMovement.y = edge ? deltaMovement.y : -math.abs(deltaMovement.x * slopeNormalPerp.y) - gripDegree;

            Debug.DrawRay(Hit[1].point, Hit[1].normal, UnityEngine.Color.red);
            Debug.DrawRay(Hit[1].point, slopeNormalPerp, UnityEngine.Color.red);

            if (deltaMovement.y < -Hit[1].distance)
                below = true;

            offset = GroundCollisinCheck(Hit[1].distance, deltaMovement);
        }
        else
        {
            slopeNormalPerp = edge ? Vector2.right : -Vector2.Perpendicular(Hit[2].normal).normalized;
            deltaMovement.y = edge ? deltaMovement.y : -math.abs(deltaMovement.x * slopeNormalPerp.y) - gripDegree;

            Debug.DrawRay(Hit[2].point, Hit[2].normal, UnityEngine.Color.red);
            Debug.DrawRay(Hit[2].point, slopeNormalPerp, UnityEngine.Color.red);

            if (deltaMovement.y < -Hit[2].distance)
                below = true;

            offset = GroundCollisinCheck(Hit[2].distance, deltaMovement);
        }

        if (slopeNormalPerp == Vector2.zero)
        {
            //离开边缘为斜坡的平台的最后一次检测，此时角色已经离开地面所以检测不到斜坡斜率，所以使用上一次检测的斜率
            deltaMovement.y = deltaMovement.x * lastSlopeNormalPerp.y + offset;
            deltaMovement.x *= lastSlopeNormalPerp.x;
        }
        else
        {
            deltaMovement.y = deltaMovement.x * slopeNormalPerp.y + offset;
            deltaMovement.x *= slopeNormalPerp.x;
        }
        //横向碰撞
        RayHorizontal(checkPos, Vector2.right * deltaMovement.x, ref Hit, horizontalRayDistance);
        HorizontalCollisionCheck(Hit, ref deltaMovement, deltaMovement.x);

        //计算地面移动时y轴的碰撞
        float GroundCollisinCheck(float distance, Vector2 deltaMovement)
        {
            if (distance < Math.Abs(deltaMovement.y))
            {
                if (distance < 0 && distance < -CollisionHardness)
                    return -Math.Sign(deltaMovement.y) * CollisionHardness;
                else
                    return distance * Math.Sign(deltaMovement.y);
            }
            return 0;
        }
    }

    void MoveVertical(ref Vector2 deltaMovement, Vector2 checkPos)
    {
        RaycastHit2D[] Hit = new RaycastHit2D[2];
        var rayDistance = (edge && deltaMovement.y < 0) ? verticalRayDistance : specialRayDistance;
        var rayDirection = deltaMovement.y != 0 ? Vector2.up * deltaMovement.y : Vector2.up;
        var moveDirection = deltaMovement.y;

        RayVertical(checkPos, rayDirection, ref Hit[0], ref Hit[1], rayDistance);
        VerticalCollisionCheck(Hit, ref deltaMovement, rayDirection.y);
        verticalSpeedReset = verticalCollided;

        if (!below && moveDirection >= 0)
        {
            rayDirection *= -1;
            RayVertical(checkPos, rayDirection, ref Hit[0], ref Hit[1], rayDistance);
            VerticalCollisionCheck(Hit, ref deltaMovement, rayDirection.y);
            verticalSpeedReset=(verticalSpeedReset==verticalCollided)?true:false;
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
        horizontalSpeedReset =(horizontalSpeedReset==horizontalCollided)?true:false;
    }

    //射线检测，并且将未命中的射线的距离设置成1000，便于后续判断
    void RaySetReturn(ref RaycastHit2D Hit, Vector2 checkPos, Vector2 direction, float size, float CheckDistance)
    {
        Debug.DrawRay(checkPos, direction.normalized * CheckDistance * size / 2);
        Hit = Physics2D.Raycast(checkPos, direction, CheckDistance * size / 2, platformMask);
        Hit.distance -= size / 2;
        if (!Hit)
            Hit.distance = 1000f;
    }

    //竖直的射线起点计算和射线检测
    void RayVertical(Vector2 checkPos, Vector2 direction, ref RaycastHit2D rayHit1, ref RaycastHit2D rayHit2, float rayDistance)
    {
        checkPos.x += rayDistance * coll.size.x / 2;
        RaySetReturn(ref rayHit1, checkPos, direction, coll.size.y, verticalCheckDistance);

        checkPos.x -= rayDistance * coll.size.x;
        RaySetReturn(ref rayHit2, checkPos, direction, coll.size.y, verticalCheckDistance);
    }

    //水平的射线起点计算和射线检测
    void RayHorizontal(Vector2 checkPos, Vector2 direction, ref RaycastHit2D[] Hit, float rayDistance)
    {
        RaySetReturn(ref Hit[0], checkPos, direction, coll.size.x, horizontalCheckDistance);

        checkPos.y += rayDistance * coll.size.y / 2;
        RaySetReturn(ref Hit[1], checkPos, direction, coll.size.x, horizontalCheckDistance);

        checkPos.y -= rayDistance * coll.size.y;
        RaySetReturn(ref Hit[2], checkPos, direction, coll.size.x, horizontalCheckDistance);
    }

    public bool GetIsGround()
    {
        return isGround;
    }
}
