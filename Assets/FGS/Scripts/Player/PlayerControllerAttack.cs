﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FGS
{
    public partial class PlayerController
    {
        [System.Serializable]
        private class WeaponData
        {
            public int damage;
            public float range;
            public float knockbackForce;

            public WeaponData(int damage, float range, float knockbackForce)
            {
                this.damage = damage;
                this.range = range;
                this.knockbackForce = knockbackForce;
            }
        }

        private static readonly Dictionary<TypeOfWeapon, WeaponData> weaponDataMap = new()
        {
            { TypeOfWeapon.Rifle,   new WeaponData(15, 25f, 1f) },
            { TypeOfWeapon.Shotgun, new WeaponData(25, 15f, 5f) }
        };


        [Header("Shooting")]
        [SerializeField] private int damagePerShot = 20;
        [SerializeField] private float singleBurstDuration = 0.5f;
        [SerializeField] private float shotgunBurstDuration = 0.5f;
        [SerializeField] private float timeBetweenBursts = 0.25f;
        [SerializeField] private float shotRange = 100f;
        [SerializeField] private TypeOfWeapon currentWeapon = TypeOfWeapon.Rifle;
        [SerializeField] private float shotgunAngle = 60f;
        [SerializeField] private float shotgunRadius = 6f;
        [SerializeField] private float knockbackForce = 5f;
        [SerializeField] private int shotgunRayCount = 5;

        [Header("Shooting FX")]
        [SerializeField] private GameObject shootingEffectObject;
        [SerializeField] private ParticleSystem gunParticles;
        [SerializeField] private LineRenderer gunLine;
        [SerializeField] private AudioSource gunAudio;
        [SerializeField] private Light gunLight;
        [SerializeField] private Light faceLight;

        private Ray shootRay;
        private int shootableMask;
        private bool isFiring;
        private Coroutine firingRoutine;
        private Coroutine disableEffectRoutine;
        public TypeOfWeapon CurrentWeapon => currentWeapon;

        private void UpdateAttack()
        {
            if (targetEnemy == null)
                return;

            HandleDamageFlash();

            if (isFiring) return;

            firingRoutine = StartCoroutine(FireRoutine());
        }


        private void HandleDamageFlash()
        {
            if (damageImage != null)
            {
                damageImage.color = isDamaged
                    ? flashColor
                    : Color.Lerp(damageImage.color, Color.clear, flashSpeed * Time.deltaTime);
            }
            isDamaged = false;
        }

        public void TakeDamage(int damage)
        {
            isDamaged = true;
            currentHealth -= damage;
            if (healthSlider != null) healthSlider.value = currentHealth;

            UIManager.Instance?.FlashScreenDamage(0.4f);

            if (currentHealth <= 0 && !isDead)
                Die();
        }


        private IEnumerator FireRoutine()
        {
            isFiring = true;

            switch (currentWeapon)
            {
                case TypeOfWeapon.Rifle:
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            PerformRaycast();
                            PlayMuzzleEffects();
                            DisableEffects(singleBurstDuration * 0.1f);
                            yield return new WaitForSeconds(singleBurstDuration / 5f);
                        }

                        yield return new WaitForSeconds(timeBetweenBursts);
                        break;
                    }
                case TypeOfWeapon.Shotgun:
                    {
                        for (int i = 0; i < 2; i++)
                        {
                            PerformShotgunBlast();
                            PlayMuzzleEffects();
                            DisableEffects(shotgunBurstDuration * 0.1f);
                            yield return new WaitForSeconds(shotgunBurstDuration / 2f);
                        }

                        yield return new WaitForSeconds(0.5f);
                        break;
                    }
            }

            isFiring = false;
        }

        private void PerformRaycast()
        {
            shootRay.origin = shootingEffectObject.transform.position;
            shootRay.direction = transform.forward;

            if (Physics.Raycast(shootRay, out var hitInfo, shotRange, shootableMask))
            {
                TryDealDamage(hitInfo);
                SetLineEnd(hitInfo.point);
            }
            else
            {
                SetLineEnd(shootRay.origin + shootRay.direction * shotRange);
            }
        }

        private void PerformShotgunBlast()
        {
            var origin = shootingEffectObject.transform.position;
            var forward = transform.forward;
            var hits = Physics.OverlapSphere(origin, shotgunRadius, shootableMask);

            foreach (var hit in hits)
            {
                if (hit.TryGetComponent<EnemyController>(out var enemy))
                {
                    var direction = (enemy.transform.position - origin).normalized;
                    var angle = Vector3.Angle(forward, direction);

                    if (angle <= shotgunAngle / 2f)
                    {
                        enemy.TakeDamage(damagePerShot, transform.position, knockbackForce);
                    }
                }
            }

            if (gunLine != null)
            {
                gunLine.positionCount = shotgunRayCount * 2;
                for (var i = 0; i < shotgunRayCount; i++)
                {
                    var angleOffset = Mathf.Lerp(-shotgunAngle / 2f, shotgunAngle / 2f, i / (float)(shotgunRayCount - 1));
                    var direction = Quaternion.Euler(0f, angleOffset, 0f) * forward;

                    gunLine.SetPosition(i * 2, origin);
                    gunLine.SetPosition(i * 2 + 1, origin + direction * shotgunRadius);
                }

                gunLine.enabled = true;
            }
        }

        private void TryDealDamage(RaycastHit hit)
        {
            if (hit.collider.TryGetComponent<EnemyController>(out var enemy))
                enemy.TakeDamage(damagePerShot, hit.point, knockbackForce);
        }

        private void SetLineEnd(Vector3 endPoint)
        {
            if (gunLine != null)
            {
                gunLine.positionCount = 2;
                gunLine.SetPosition(1, endPoint);
            }
        }

        private void PlayMuzzleEffects()
        {
            if (gunLine != null) gunLine.enabled = true;
            if (gunLight != null) gunLight.enabled = true;
            if (faceLight != null) faceLight.enabled = true;

            gunAudio?.Play();
            gunParticles?.Stop();
            gunParticles?.Play();
            gunLine?.SetPosition(0, shootingEffectObject.transform.position);
        }

        private void DisableEffects(float delay)
        {
            RestartCoroutine(ref disableEffectRoutine, DisableEffectsAsync(delay));
        }

        public void DisableEffects()
        {
            if (gunLine != null) gunLine.enabled = false;
            if (gunLight != null) gunLight.enabled = false;
            if (faceLight != null) faceLight.enabled = false;
        }

        private IEnumerator DisableEffectsAsync(float delay)
        {
            yield return new WaitForSeconds(delay);
            DisableEffects();
        }

        private void RestartCoroutine(ref Coroutine routine, IEnumerator coroutine)
        {
            if (routine != null)
                StopCoroutine(routine);
            routine = StartCoroutine(coroutine);
        }

        public TypeOfWeapon SwitchWeapon()
        {
            currentWeapon = currentWeapon switch
            {
                TypeOfWeapon.Rifle => TypeOfWeapon.Shotgun,
                TypeOfWeapon.Shotgun => TypeOfWeapon.Rifle,
                _ => TypeOfWeapon.Rifle
            };

            ApplyWeaponData(currentWeapon);
            return currentWeapon;
        }

        private void ApplyWeaponData(TypeOfWeapon weapon)
        {
            if (!weaponDataMap.TryGetValue(weapon, out var data))
            {
                Debug.LogWarning($"Weapon data not found for: {weapon}");
                return;
            }

            damagePerShot = data.damage;
            shotRange = data.range;
            knockbackForce = data.knockbackForce;
        }

    }
}