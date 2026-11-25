import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { apiError, TravelApi } from './api';
import { airports, personas, savePreferences } from './preferences';

@Component({
  imports: [RouterLink],
  templateUrl: './auth-page.html',
})
export class AuthPage {
  private readonly api = inject(TravelApi);
  private readonly router = inject(Router);
  readonly signup = inject(ActivatedRoute).snapshot.data['signup'] === true;
  readonly busy = signal(false);
  readonly error = signal('');
  readonly challenge = signal<{ challengeId: string; developmentCode?: string } | null>(null);
  readonly digits = [0, 1, 2, 3, 4, 5];
  readonly otp = signal('');
  readonly phoneCount = signal(0);
  readonly resendSeconds = signal(0);
  readonly airports = airports;
  readonly personas = personas;
  readonly airport = signal<string>('BLR');
  readonly persona = signal<string>('Slow & Scenic');
  private resendBy = 0;
  phone = '';

  constructor() {
    const timer = setInterval(() => this.resendSeconds.set(Math.max(0, Math.ceil((this.resendBy - Date.now()) / 1000))), 250);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));
  }

  get maskedPhone() {
    return `+91 ••••• ${this.phone.slice(-5)}`;
  }

  async submit(event: SubmitEvent) {
    event.preventDefault();
    if (this.busy()) return;
    const form = new FormData(event.currentTarget as HTMLFormElement);
    if (!this.signup && !this.challenge()) {
      this.phone = '+91' + String(form.get('phone') ?? '').replace(/\D/g, '');
      await this.sendCode();
      return;
    }

    await this.run(async () => {
      if (this.signup) {
        await this.api.signup(String(form.get('displayName') ?? '').trim());
        const user = this.api.user();
        if (user) savePreferences(user.id, this.airport(), this.persona());
        await this.router.navigateByUrl('/dashboard');
        return;
      }

      const challenge = this.challenge();
      if (!challenge) return;
      const verification = await this.api.verifyOtp(challenge.challengeId, this.otp());
      await this.router.navigateByUrl(verification.status === 'signup_required' ? '/signup' : '/dashboard');
    });
  }

  async sendCode() {
    await this.run(async () => {
      this.challenge.set(await this.api.requestOtp(this.phone));
      this.otp.set('');
      this.resendBy = Date.now() + 30000;
      this.resendSeconds.set(30);
    });
  }

  changeNumber() {
    this.challenge.set(null);
    this.error.set('');
  }

  updateDigits(event: Event, isPhone = false) {
    const input = event.target as HTMLInputElement;
    const digits = input.value.replace(/\D/g, '');
    const value = (isPhone && digits.length === 12 && digits.startsWith('91') ? digits.slice(2) : digits)
      .slice(0, isPhone ? 10 : 6);
    input.value = value;
    if (isPhone) this.phoneCount.set(value.length);
    else this.otp.set(value);
  }

  private async run(action: () => Promise<void>) {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    try {
      await action();
    } catch (error) {
      this.error.set(error instanceof HttpErrorResponse && error.status === 404
        ? 'Phone sign-in is unavailable on this deployment. You can still explore the planner.'
        : apiError(error));
    } finally {
      this.busy.set(false);
    }
  }
}
