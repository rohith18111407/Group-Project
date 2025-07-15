import { Component, OnInit } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuthService } from './services/auth.service';
import { HeaderComponent } from "./header/header";

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, CommonModule, HeaderComponent],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit {
  constructor(private auth: AuthService, private router: Router) { }

  ngOnInit(): void {
    if (this.auth.isLoggedIn()) {
      this.auth.setupTokenMonitor(); //  Setup token expiry watcher
    }
  }

  isLoggedIn(): boolean {
    return !!this.auth.getAccessToken();
  }

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/login']);
  }
}
