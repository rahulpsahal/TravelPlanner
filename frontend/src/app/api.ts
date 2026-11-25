import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface User {
  id: string;
  displayName: string;
  phoneNumber: string;
}

export interface TripRequest {
  origin: string;
  destination: string;
  departureDate: string;
  returnDate: string;
  travelers: number;
  budget: number;
}

export interface TripOption {
  flight: { name: string; providerSearchUrl: string };
  hotel: { name: string; providerSearchUrl: string };
  transportTotal: number;
  lodgingTotal: number;
  total: number;
  budgetDifference: number;
}

export interface TripPreview extends TripRequest {
  dataSource: string;
  currency: string;
  nights: number;
  rooms: number;
  hasWithinBudgetOptions: boolean;
  options: TripOption[];
}

export interface HotelResults {
  provider: string;
  results: { title: string | null; url: string; content: string | null }[];
}

interface Airport {
  airport: string | null;
  iata: string | null;
  scheduled: string | null;
  actual: string | null;
}

export interface FlightResults {
  provider: string;
  results: {
    flightCode: string | null;
    flightDate: string | null;
    status: string | null;
    airline: string | null;
    departure: Airport | null;
    arrival: Airport | null;
  }[];
}

export interface TrainFare {
  provider: string;
  status: string;
  trainName: string | null;
  trainNumber: string;
  source: string;
  destination: string;
  travelClass: string;
  date: string;
  totalFare: number | null;
  bookingSearchUrl: string;
}

export interface Itinerary {
  provider: string;
  itinerary: string;
}

@Injectable({ providedIn: 'root' })
export class TravelApi {
  private readonly http = inject(HttpClient);
  private readonly account = signal<User | null>(null);
  readonly user = this.account.asReadonly();

  requestOtp(phoneNumber: string) {
    return firstValueFrom(this.http.post<{
      challengeId: string;
      expiresInSeconds: number;
      developmentCode?: string;
    }>('/api/auth/otp/request', { phoneNumber }));
  }

  verifyOtp(challengeId: string, code: string) {
    return firstValueFrom(this.http.post<{ status: 'authenticated' | 'signup_required' }>(
      '/api/auth/otp/verify', { challengeId, code }));
  }

  async signup(displayName: string) {
    const user = await firstValueFrom(this.http.post<User>('/api/auth/signup', { displayName }));
    this.account.set(user);
  }

  async currentUser() {
    try {
      const user = await firstValueFrom(this.http.get<User>('/api/auth/me'));
      this.account.set(user);
      return user;
    } catch {
      this.account.set(null);
      return null;
    }
  }

  async logout() {
    await firstValueFrom(this.http.post<void>('/api/auth/logout', null));
    this.account.set(null);
  }

  preview(trip: TripRequest) {
    return firstValueFrom(this.http.post<TripPreview>('/api/trips/preview', trip));
  }

  hotels(destination: string) {
    return firstValueFrom(this.http.get<HotelResults>('/api/travel/hotels', { params: { destination } }));
  }

  flights(params: Record<string, string>) {
    return firstValueFrom(this.http.get<FlightResults>('/api/travel/flights', { params }));
  }

  trainFare(trainNumber: string, params: Record<string, string>) {
    return firstValueFrom(this.http.get<TrainFare>(
      `/api/travel/trains/${encodeURIComponent(trainNumber)}/fare`, { params }));
  }

  itinerary(destination: string, days: number, interests: string) {
    return firstValueFrom(this.http.post<Itinerary>(
      '/api/travel/itinerary', { destination, days, interests }));
  }

  copilot(question: string, destination: string) {
    return firstValueFrom(this.http.post<{ answer: string }>(
      '/api/travel/copilot', { question, destination }));
  }
}

export function apiError(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) return 'Something went wrong. Please try again.';
  if (error.status === 0) return 'Could not reach Travel Planner. Check your connection and try again.';
  if (error.status === 429) return error.error?.detail || 'Too many requests. Wait a moment and try again.';
  const fields = error.error?.errors as Record<string, string[]> | undefined;
  return (fields && Object.values(fields).flat().join(' '))
    || error.error?.detail || error.error?.title || 'The request could not be completed.';
}
