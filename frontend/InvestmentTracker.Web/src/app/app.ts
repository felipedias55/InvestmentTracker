import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { HealthService } from './core/services/health.service';

@Component({
  selector: 'app-root',
  standalone: true,
  templateUrl: './app.html',
  styleUrl: './app.css',
  imports: [RouterOutlet]
})
export class App implements OnInit {

  private readonly healthService = inject(HealthService);

  apiStatus = 'Verificando API...';
  title = 'Investment Tracker';

  ngOnInit(): void {
    this.healthService.getHealth().subscribe({
      next: response => {
        this.apiStatus = `${response.status} - ${response.application}`;
      },
      error: error => {
        console.error(error);
        this.apiStatus = 'API indisponível';
      }
    });
  }
}
