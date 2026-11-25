import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { apiError, FlightResults, HotelResults, Itinerary, TrainFare, TravelApi, TripOption, TripPreview } from './api';
import { preferences } from './preferences';

type View = 'plan' | 'hotels' | 'flights' | 'train' | 'itinerary';
type Screen = 'discover' | 'planner' | 'itinerary' | 'copilot' | 'wallet';
interface Activity { time: string; title: string; detail: string }

@Component({
  imports: [CurrencyPipe, DatePipe, RouterLink],
  templateUrl: './planner-page.html',
})
export class PlannerPage {
  private readonly api = inject(TravelApi);
  private readonly router = inject(Router);
  readonly publicPreview = inject(ActivatedRoute).snapshot.data['preview'] === true;
  readonly user = this.api.user;
  readonly profile = computed(() => preferences(this.user()?.id));
  readonly screen = signal<Screen>('discover');
  readonly currentView = signal<View>('itinerary');
  readonly destination = signal('');
  readonly days = signal(5);
  readonly companion = signal('Couple');
  readonly budgetTier = signal('Balanced');
  readonly pace = signal('Moderate');
  readonly interestOptions = ['⛩ Temples', '🍵 Local Cafés', '✦ Hidden Alleys', '🏮 Night Markets'];
  readonly interests = signal<string[]>([]);
  readonly day = signal(0);
  readonly itineraryTab = signal<'timeline' | 'packing'>('timeline');
  readonly itineraryTrip = signal<{ destination: string; days: number } | null>(null);
  readonly packing = signal<string[]>([]);
  readonly packingItems = ['Photo ID', 'Phone charger', 'Walking shoes', 'Travel documents'];
  readonly picked = signal<TripOption | null>(null);
  readonly messages = signal<{ question: string; answer: string }[]>([]);
  readonly chatInput = signal('');
  readonly activitySwaps = signal<Record<string, string>>({});
  readonly itineraryDays = computed(() => {
    const days: Activity[][] = [[]];
    let activeDay = 0;
    for (const line of (this.itinerary()?.itinerary ?? '').split('\n')) {
      const clean = line.replace(/^[#*\s-]+|[*\s]+$/g, '');
      const match = /^Day\s+(\d+)\b/i.exec(clean);
      if (match) {
        const number = Number(match[1]);
        if (number >= 1 && number <= 14) activeDay = number - 1;
      } else if (clean) {
        const [time, title, ...detail] = clean.split('|').map(part => part.trim());
        (days[activeDay] ??= []).push(title ? { time, title, detail: detail.join(' | ') } : { time: '', title: clean, detail: '' });
      }
      days[activeDay] ??= [];
    }
    return Array.from(days, activities => activities ?? []);
  });
  readonly busy = signal(false);
  readonly signingOut = signal(false);
  readonly error = signal('');
  readonly sessionError = signal('');
  readonly trip = signal<TripPreview | null>(null);
  readonly hotels = signal<HotelResults | null>(null);
  readonly flights = signal<FlightResults | null>(null);
  readonly fare = signal<TrainFare | null>(null);
  readonly itinerary = signal<Itinerary | null>(null);
  readonly today = this.dateFromToday(0);
  readonly departure = this.dateFromToday(7);
  readonly returnDate = this.dateFromToday(10);
  readonly views = [
    { id: 'itinerary', name: 'AI itinerary' },
    { id: 'plan', name: 'Compare' },
    { id: 'hotels', name: 'Stays' },
    { id: 'flights', name: 'Flights' },
    { id: 'train', name: 'Trains' },
  ] as const;
  readonly screens = [
    { id: 'discover', name: 'Discover', icon: '⊙' },
    { id: 'planner', name: 'Planner', icon: '⚙' },
    { id: 'itinerary', name: 'Itinerary', icon: '⚑' },
    { id: 'copilot', name: 'Co-Pilot', icon: '✦' },
    { id: 'wallet', name: 'Wallet', icon: '▣' },
  ] as const;

  open(screen: Screen) {
    if (this.busy()) return;
    this.screen.set(screen);
    this.error.set('');
    window.scrollTo(0, 0);
  }

  plan(destination = this.destination()) {
    this.destination.set(destination.trim());
    this.select('itinerary');
    this.open('planner');
  }

  toggle(list: string[], item: string) {
    return list.includes(item) ? list.filter(value => value !== item) : [...list, item];
  }

  select(view: View) {
    if (this.busy()) return;
    this.currentView.set(view);
    this.error.set('');
  }

  async submit(event: SubmitEvent) {
    event.preventDefault();
    if (this.busy()) return;
    const fields = new FormData(event.currentTarget as HTMLFormElement);
    const text = (name: string) => String(fields.get(name) ?? '').trim();
    this.error.set('');
    this.busy.set(true);
    try {
      switch (this.currentView()) {
        case 'plan':
          this.trip.set(null);
          this.picked.set(null);
          this.trip.set(await this.api.preview({
            origin: text('origin'), destination: text('destination'),
            departureDate: text('departureDate'), returnDate: text('returnDate'),
            travelers: Number(text('travelers')), budget: Number(text('budget')),
          }));
          break;
        case 'hotels':
          this.hotels.set(null);
          this.hotels.set(await this.api.hotels(text('destination')));
          break;
        case 'flights': {
          this.flights.set(null);
          const code = text('flightCode');
          this.flights.set(await this.api.flights(code
            ? { flightCode: code }
            : { origin: text('origin'), destination: text('destination') }));
          break;
        }
        case 'train':
          this.fare.set(null);
          this.fare.set(await this.api.trainFare(text('trainNumber'), {
            source: text('source'), destination: text('destination'),
            travelClass: text('travelClass'), date: text('date'), quota: text('quota'),
          }));
          break;
        case 'itinerary': {
          const destination = text('destination');
          const days = this.days();
          const preferences = [this.companion(), this.budgetTier(), this.pace(), ...this.interests()].join(', ');
          this.itinerary.set(await this.api.itinerary(destination, days, preferences));
          this.destination.set(destination);
          this.itineraryTrip.set({ destination, days });
          this.itineraryTab.set('timeline');
          this.activitySwaps.set({});
          this.day.set(0);
          this.screen.set('itinerary');
          window.scrollTo(0, 0);
          break;
        }
      }
    } catch (error) {
      this.error.set(apiError(error));
    } finally {
      this.busy.set(false);
    }
  }

  async ask(event: SubmitEvent) {
    event.preventDefault();
    const question = this.chatInput().trim();
    if (this.busy() || question.length < 2) return;
    this.error.set('');
    this.busy.set(true);
    try {
      const { answer } = await this.api.copilot(question, this.itineraryTrip()?.destination ?? '');
      this.messages.update(items => [...items, { question, answer }]);
      this.chatInput.set('');
    } catch (error) {
      this.error.set(apiError(error));
    } finally {
      this.busy.set(false);
    }
  }

  async swap(activity: Activity, index: number) {
    if (this.busy()) return;
    this.error.set('');
    this.busy.set(true);
    try {
      const key = `${this.day()}-${index}`;
      const question = `Suggest one nearby replacement for this itinerary activity: ${activity.title}`.slice(0, 500);
      const { answer } = await this.api.copilot(question, this.itineraryTrip()?.destination ?? '');
      this.activitySwaps.update(swaps => ({ ...swaps, [key]: answer }));
    } catch (error) {
      this.error.set(apiError(error));
    } finally {
      this.busy.set(false);
    }
  }

  choose(option: TripOption) {
    this.picked.set(option);
    this.open('wallet');
  }

  async signOut() {
    if (this.signingOut()) return;
    this.signingOut.set(true);
    this.sessionError.set('');
    try {
      await this.api.logout();
      await this.router.navigateByUrl('/login');
    } catch (error) {
      this.sessionError.set(apiError(error));
    } finally {
      this.signingOut.set(false);
    }
  }

  private dateFromToday(days: number) {
    const indiaDate = new Date(Date.now() + (330 * 60_000) + (days * 86_400_000));
    return indiaDate.toISOString().slice(0, 10);
  }
}
